namespace NEventSocket.Tests.Sockets
{
    using System;
    using System.Net.Sockets;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Logging;

    using NUnit.Framework;

    using NEventSocket;
    using NEventSocket.Logging;
    using NEventSocket.Tests.Fakes;
    using NEventSocket.Tests.TestSupport;

    [TestFixture]
    public class OutboundListenerTests
    {
        [SetUp]
        public void SetUp()
        {
            PreventThreadPoolStarvation.Init();
            Logger.Configure(LoggerFactory.Create(builder =>
            {
                builder
                    .AddFilter("Microsoft", LogLevel.Warning)
                    .AddFilter("System", LogLevel.Warning)
                    .AddFilter("LoggingConsoleApp.Program", LogLevel.Debug)
                    .AddConsole();
            }));
        }

        [Test, CancelAfter(2000)]
        public void Disposing_the_listener_completes_the_connections_observable()
        {
            using (var listener = new OutboundListener(0))
            {
                listener.Start();

                bool completed = false;

                listener.Connections.Subscribe(_ => { }, () => completed = true);

                listener.Dispose();

                Assert.That(completed, Is.True);
            }
        }

        [Test, CancelAfter(2000)]
        public async Task Disposing_the_listener_disposes_any_connected_clients()
        {
            using (var listener = new OutboundListener(0))
            {
                listener.Start();

                bool connected = false;
                bool disposed = false;

                listener.Connections.Subscribe((socket) =>
                {
                    connected = true;
                    socket.Disposed += (o, e) => disposed = true;
                });

               var client = new FakeFreeSwitchSocket(listener.Port);

                await Wait.Until(() => connected);
                listener.Dispose();

                Assert.That(disposed, Is.True);
            }
        }

        [Test, CancelAfter(2000)]
        public async Task Stopping_the_listener_does_not_dispose_any_connected_clients()
        {
            using (var listener = new OutboundListener(0))
            {
                listener.Start();

                bool connected = false;
                bool disposed = false;

                listener.Connections.Subscribe((socket) =>
                {
                    connected = true;
                    socket.Disposed += (o, e) => disposed = true;
                });

                var _ = new FakeFreeSwitchSocket(listener.Port);

                await Wait.Until(() => connected);

                listener.Stop();

                Assert.That(disposed, Is.False);

                listener.Dispose();
                Assert.That(disposed, Is.True);
            }
        }

        [Test, CancelAfter(2000)]
        public async Task Can_restart_the_listener_after_stopping()
        {
            using (var listener = new OutboundListener(0))
            {
                listener.Start();
                int counter = 0;

                listener.Connections.Subscribe((socket) =>
                {
                    counter++;
                });

                new FakeFreeSwitchSocket(listener.Port);
                await Wait.Until(() => counter == 1);
                listener.Stop();

                //not listening
                Assert.Throws<SocketException>(() => new FakeFreeSwitchSocket(listener.Port));

                listener.Start();
                new FakeFreeSwitchSocket(listener.Port);
                await Wait.Until(() => counter == 2);
            }
        }

        [Test, CancelAfter(2000)]
        public async Task a_new_connection_produces_an_outbound_socket()
        {
            using (var listener = new OutboundListener(0))
            {
                listener.Start();

                bool connected = false;

                listener.Connections.Subscribe((socket) => connected = true);

                var client = new FakeFreeSwitchSocket(listener.Port);

                await Wait.Until(() => connected);
                Assert.That(connected, Is.True);
            }
        }

        [Test, CancelAfter(2000)]
        public async Task each_new_connection_produces_a_new_outbound_socket_from_the_Connections_observable()
        {
            const int NumberOfConnections = 3;

            using (var listener = new OutboundListener(0))
            {
                listener.Start();

                var connected = 0;

                listener.Connections.Subscribe((socket) => connected++);

                for (int i = 0; i < NumberOfConnections; i++)
                {
                    var client = new FakeFreeSwitchSocket(listener.Port);
                }

                await Wait.Until(() => connected == NumberOfConnections);
                Assert.That(connected, Is.EqualTo(NumberOfConnections));
            }
        }

        [Test, CancelAfter(TimeOut.TestTimeOutMs)]
        public async Task ProblematicSocket_connect_errors_should_not_cause_subsequent_connections_to_fail()
        {
            var connectionsHandled = 0;
            var observableCompleted = false;

            using (var listener = new ProblematicListener(0))
            {
                listener.Start();


                listener.Connections.Subscribe(_ => connectionsHandled++, ex => { }, () => observableCompleted = true);

                using (var client = new FakeFreeSwitchSocket(listener.Port))
                {
                    await Wait.Until(() => ProblematicListener.Counter == 1);
                    Assert.That(connectionsHandled, Is.EqualTo(0));
                    Assert.That(ProblematicListener.Counter, Is.EqualTo(1));
                    Assert.That(observableCompleted, Is.False);
                }

                using (var client = new FakeFreeSwitchSocket(listener.Port))
                {
                    await Wait.Until(() => connectionsHandled == 1);
                    Assert.That(connectionsHandled, Is.EqualTo(1));
                    Assert.That(ProblematicListener.Counter, Is.EqualTo(2));
                    Assert.That(observableCompleted, Is.False);
                }
            }

            Assert.That(observableCompleted, Is.True);
        }

        [Test, CancelAfter(2000)]
        public void IsStarted_is_false_when_initialized()
        {
            using (var listener = new OutboundListener(0))
            {
                Assert.That(listener.IsStarted, Is.False);
            }
        }

        [Test, CancelAfter(2000)]
        public void IsStarted_is_true_when_started()
        {
            using (var listener = new OutboundListener(0))
            {
                listener.Start();
                Assert.That(listener.IsStarted, Is.True);
            }
        }

        [Test, CancelAfter(2000)]
        public void IsStarted_is_false_when_stopped()
        {
            using (var listener = new OutboundListener(0))
            {
                listener.Start();
                Assert.That(listener.IsStarted, Is.True);

                listener.Stop();
                Assert.That(listener.IsStarted, Is.False);
            }
        }

        [Test, CancelAfter(2000)]
        public async Task IsStarted_is_false_when_disposed()
        {
            using (var listener = new OutboundListener(0))
            {
                listener.Start();

                listener.Dispose();
                await Wait.Until(() => listener.IsStarted == false);
            }
        }

        [Test, CancelAfter(2000)]
        public void Starting_should_be_idempotent()
        {
            using (var listener = new OutboundListener(0))
            {
                listener.Start();
                listener.Start();
                Assert.That(listener.IsStarted, Is.True);
            }
        }
    }
}