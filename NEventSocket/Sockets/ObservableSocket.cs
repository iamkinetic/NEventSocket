﻿using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Reactive.PlatformServices;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NEventSocket.Logging;
using NEventSocket.Util;
using NEventSocket.Util.ObjectPooling;

// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ObservableSocket.cs" company="Dan Barua">
//   (C) Dan Barua and contributors. Licensed under the Mozilla Public License.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------
namespace NEventSocket.Sockets
{ 
    /// <summary>
    /// Wraps a <seealso cref="TcpClient"/> exposing incoming strings as an Observable sequence.
    /// </summary>
    public abstract class ObservableSocket : IDisposable
    {
        private static long IdCounter = 0;
        protected readonly long id;
        private readonly ILogger Log;
        private readonly SemaphoreSlim syncLock = new SemaphoreSlim(1);
        private readonly InterlockedBoolean disposed = new InterlockedBoolean();
        private readonly InterlockedBoolean isStarted = new InterlockedBoolean();
        private readonly CancellationTokenSource readCancellationToken = new CancellationTokenSource();
        private TcpClient tcpClient;
        private readonly SubjectBase<byte[]> subject;
        private IObservable<byte[]> receiver;
        
        static ObservableSocket()
        {
            //we need this to work around issues ilmerging rx assemblies
#pragma warning disable 618
            PlatformEnlightenmentProvider.Current = new CurrentPlatformEnlightenmentProvider();
#pragma warning restore 618
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ObservableSocket"/> class.
        /// </summary>
        /// <param name="tcpClient">The TCP client to wrap.</param>
        protected ObservableSocket(TcpClient tcpClient)
        {
            Log = Logger.Get<ObservableSocket>();

            id = Interlocked.Increment(ref IdCounter);

            this.tcpClient = tcpClient;

            subject = new NotifyingSubject<byte[]>(SubscriberAdded);
            receiver = subject.AsObservable();

            void SubscriberAdded()
            {
                if (isStarted.EnsureCalledOnce())
                {
                    return;
                }

                Task.Run(
                    async () =>
                    {
                        SafeLog(LogLevel.Trace, "{0} Worker Task {1} started".Fmt(GetType(), id));

                        int bytesRead = 1;
                        var stream = tcpClient.GetStream();
                        byte[] buffer = SharedPools.ByteArray.Allocate();
                        try
                        {
                            while (bytesRead > 0)
                            {
                                bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, readCancellationToken.Token);
                                if (bytesRead > 0)
                                {
                                    if (bytesRead == buffer.Length)
                                    {
                                        subject.OnNext(buffer);
                                    }
                                    else
                                    {
                                        subject.OnNext(buffer.Take(bytesRead).ToArray());
                                    }
                                }
                                else
                                {
                                    subject.OnCompleted();
                                }
                            }
                        }
                        catch (ObjectDisposedException)
                        {
                            //expected - normal shutdown
                            subject.OnCompleted();
                        }
                        catch (TaskCanceledException)
                        {
                            //expected - normal shutdown
                            subject.OnCompleted();
                        }
                        catch (IOException ex)
                        {
                            if (ex.InnerException is ObjectDisposedException)
                            {
                                //expected - normal shutdown
                                subject.OnCompleted();
                            }
                            else
                            {
                                //socket comms interrupted - propagate the error up the layers
                                SafeLog(LogLevel.Error, "IO Error reading from stream", ex);
                                    subject.OnError(ex);
                            }
                        }
                        catch (SocketException ex)
                        {
                            //socket comms interrupted - propagate the error up the layers
                            SafeLog(LogLevel.Error, "Socket Error reading from stream", ex);
                                subject.OnError(ex);
                        }
                        catch (OperationCanceledException ex)
                        {
                            if (ex.CancellationToken.IsCancellationRequested)
                            {
                                subject.OnCompleted();
                            }
                            else
                            {
                                SafeLog(LogLevel.Error,
                                        "OperationCancelled not Requested by app", ex);
                                subject.OnError(ex);
                            }
                        }
                        catch (Exception ex)
                        {
                            //unexpected error
                            SafeLog(LogLevel.Error, "Unexpected Error reading from stream", ex);
                                subject.OnError(ex);
                        }
                        finally
                        {
                            SharedPools.ByteArray.Free(buffer);

                            SafeLog(LogLevel.Trace, "{0} Worker Thread {1} completed".Fmt(GetType(), id));
                            Dispose();
                        }
                    });

            }
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="ObservableSocket"/> class.
        /// </summary>
        ~ObservableSocket()
        {
            Dispose(false);
        }

        /// <summary>
        /// Occurs when the <see cref="ObservableSocket"/> is disposed.
        /// </summary>
        public event EventHandler Disposed = (sender, args) => { };

        /// <summary>
        /// Gets a value indicating whether this instance is connected.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is connected; otherwise, <c>false</c>.
        /// </value>
        public bool IsConnected => tcpClient != null && tcpClient.Connected;

        /// <summary>
        /// Gets an Observable sequence of byte array chunks as read from the socket stream.
        /// </summary>
        protected IObservable<byte[]> Receiver => receiver;

        /// <summary>
        /// Asynchronously writes the given message to the socket.
        /// </summary>
        /// <param name="message">The string message to send</param>
        /// <param name="cancellationToken">A CancellationToken to cancel the send operation.</param>
        /// <returns>A Task.</returns>
        /// <exception cref="ObjectDisposedException">If disposed.</exception>
        /// <exception cref="InvalidOperationException">If not connected.</exception>
        public Task SendAsync(string message, CancellationToken cancellationToken)
        {
            return SendAsync(Encoding.ASCII.GetBytes(message), cancellationToken);
        }

        /// <summary>
        /// Asynchronously writes the given bytes to the socket.
        /// </summary>
        /// <param name="bytes">The raw byts to stream through the socket.</param>
        /// <param name="cancellationToken">A CancellationToken to cancel the send operation.</param>
        /// <returns>A Task.</returns>
        /// <exception cref="ObjectDisposedException">If disposed.</exception>
        /// <exception cref="InvalidOperationException">If not connected.</exception>
        public async Task SendAsync(byte[] bytes, CancellationToken cancellationToken)
        {
            if (disposed.Value)
            {
                throw new ObjectDisposedException(ToString());
            }

            if (!IsConnected)
            {
                throw new InvalidOperationException("Not connected");
            }

            try
            {
                await syncLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                var stream = GetStream();
                await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                SafeLog(LogLevel.Warning, "Network Stream Disposed.");
                Dispose();
            }
            catch (TaskCanceledException)
            {
                SafeLog(LogLevel.Warning, "Write operation was cancelled.");
                Dispose();
            }
            catch (IOException ex)
            {
                if (ex.InnerException is SocketException
                    && ((SocketException)ex.InnerException).SocketErrorCode == SocketError.ConnectionAborted)
                {
                    SafeLog(LogLevel.Warning, "Socket disconnected");
                    Dispose();
                    return;
                }

                throw;
            }
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode == SocketError.ConnectionAborted)
                {
                    SafeLog(LogLevel.Warning, "Socket disconnected");
                    Dispose();
                    return;
                }

                throw;
            }
            catch (Exception ex)
            {
                SafeLog(LogLevel.Error, "Error writing", ex);
                Dispose();
                throw;
            }
            finally
            {
                syncLock.Release();
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Gets the underlying network stream.
        /// </summary>
        protected virtual Stream GetStream()
        {
            return tcpClient.GetStream();
        }

        
        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed",
            MessageId = "received", 
            Justification = "received is disposed of asynchronously, when the buffer has been flushed out by the consumers")]
        protected virtual void Dispose(bool disposing)
        {
            if (disposed != null && !disposed.EnsureCalledOnce())
            {
                if (disposing)
                {
                    SafeLog(LogLevel.Trace, "Disposing {0} (disposing:{1})".Fmt(GetType(), disposing));
                }

                if (IsConnected)
                {
					readCancellationToken.Cancel();

                    if (tcpClient != null)
                    {
                        tcpClient.Close();
                        tcpClient = null;

                        SafeLog(LogLevel.Trace, "TcpClient closed");
                    }
                }

                var localCopy = Disposed;
                localCopy?.Invoke(this, EventArgs.Empty);

                SafeLog(LogLevel.Debug, "{0} ({1}) Disposed".Fmt(GetType(), id));
                
            }
        }


        private void SafeLog(LogLevel logLevel, string message, Exception exception = null, params object[] formatParameters)
        {
            Log?.Log(logLevel, exception, message, formatParameters);
        }
    }

    /// <summary>
    /// A subject that calls an `Action` when a new subscriber subscribed to it.
    /// </summary>
    sealed class NotifyingSubject<T> : SubjectBase<T> {
        readonly Action subscriberAdded;
        readonly Subject<T> inner = new Subject<T>();

        public NotifyingSubject(Action subscriberAdded)
        {
            this.subscriberAdded = subscriberAdded;
        }

        public override void Dispose()
        {
            inner.Dispose();
        }

        public override void OnCompleted()
        {
            inner.OnCompleted();
        }

        public override void OnError(Exception error)
        {
            inner.OnError(error);
        }

        public override void OnNext(T value)
        {
            inner.OnNext(value);
        }

        public override IDisposable Subscribe(IObserver<T> observer)
        {
            var r = inner.Subscribe(observer);
            subscriberAdded();
            return r;
        }

        public override bool HasObservers => inner.HasObservers;

        public override bool IsDisposed => inner.IsDisposed;
    }
};