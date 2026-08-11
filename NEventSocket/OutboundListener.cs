// --------------------------------------------------------------------------------------------------------------------
// <copyright file="OutboundListener.cs" company="Dan Barua">
//   (C) Dan Barua and contributors. Licensed under the Mozilla Public License.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------
namespace NEventSocket
{
    using System;
    using System.Net;
    using System.Reactive.Linq;

    using Microsoft.Extensions.Logging;

    using NEventSocket.Channels;
    using NEventSocket.Logging;
    using NEventSocket.Sockets;

    /// <summary>
    /// Listens for Outbound connections from FreeSwitch, providing notifications via the Connections observable.
    /// </summary>
    public class OutboundListener : ObservableListener<OutboundSocket>
    {
        private static readonly ILogger Log = Logger.Get<OutboundListener>();

        private readonly IObservable<Channel> channels;

        /// <summary>
        /// Initializes a new OutboundListener on the given address and port.
        /// </summary>
        /// <param name="address">The TCP address to listen on.</param>
        /// <param name="port">The TCP port to listen on. Pass 0 to auto-assign a dynamic port.</param>
        public OutboundListener(IPAddress address, int port) : base(address, port, tcpClient => new OutboundSocket(tcpClient))
        {
            channels = CreateChannels();
        }

        /// <summary>
        /// Initializes a new OutboundListener on the given port.
        /// Pass 0 as the port to auto-assign a dynamic port. Usually used for testing.
        /// </summary>
        /// <param name="port">The Tcp port to listen on.</param>
        public OutboundListener(int port) : base(port, tcpClient => new OutboundSocket(tcpClient))
        {
            channels = CreateChannels();
        }

        private IObservable<Channel> CreateChannels() =>
            Connections.SelectMany(
                async socket =>
                {
                    await socket.Connect().ConfigureAwait(false);
                    return await Channel.Create(socket).ConfigureAwait(false);
                });

        /// <summary>
        /// Gets an observable sequence of incoming calls wrapped as <seealso cref="Channel"/> abstractions.
        /// </summary>
        public IObservable<Channel> Channels
        {
            get
            {
                //if there is an error connecting the channel, eg. FS hangs up and goes away
                //before we can do the connect/channel_data handshake
                //then carry on allowing new connections
                return channels
                    .Do(_ => { }, ex => Log.LogError(ex, "Unable to connect Channel"))
                    .Retry();
            }
        }
    }
}