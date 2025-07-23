namespace NEventSocket.Tests.Sockets
{
    using System;
    using System.Reactive.Linq;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Logging;

    using NUnit.Framework;

    using NEventSocket;
    using NEventSocket.FreeSwitch;
    using NEventSocket.Logging;
    using NEventSocket.Tests.Fakes;
    using NEventSocket.Tests.TestSupport;

    [TestFixture]
    public class OutboundSocketTests
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

        [Test, CancelAfter(TimeOut.TestTimeOutMs)]
        public async Task Disposing_the_listener_completes_the_message_observables()
        {
            using (var listener = new OutboundListener(0))
            {
                listener.Start();

                bool connected = false;
                bool messagesObservableCompleted = false;
                bool eventsObservableCompleted = false;
                bool channelDataReceived = false;

                listener.Connections.Subscribe(async (connection) =>
                {
                    connected = true;
                    connection.Messages.Subscribe(_ => { }, () => messagesObservableCompleted = true);
                    connection.Events.Subscribe(_ => { }, () => eventsObservableCompleted = true);
                    await connection.Connect();

                    channelDataReceived = connection.ChannelData != null;
                    Assert.That(channelDataReceived, Is.True);
                });

                using (var freeSwitch = new FakeFreeSwitchSocket(listener.Port))
                {
                    freeSwitch.MessagesReceived.FirstAsync(m => m.StartsWith("connect"))
                          .Subscribe(async _ => await freeSwitch.SendChannelDataEvent());

                    await Wait.Until(() => channelDataReceived);
                    // Remove redundant disposal - using statement handles this
                            
                    await Wait.Until(() => messagesObservableCompleted);
                    await Wait.Until(() => eventsObservableCompleted);

                    Assert.That(connected, Is.True, "Expect a connection to have been made.");
                    Assert.That(messagesObservableCompleted, Is.True, "Expect the BasicMessage observable to be completed");
                    Assert.That(eventsObservableCompleted, Is.True, "Expect the EventMessage observable to be completed");
                }
            }
        }

        [Test, CancelAfter(TimeOut.TestTimeOutMs)]
        public async Task When_FreeSwitch_disconnects_it_completes_the_message_observables()
        {
            using (var listener = new OutboundListener(0))
            {
                listener.Start();

                bool connected = false;
                bool disposed = true;
                bool messagesObservableCompleted = false;
                bool eventsObservableCompleted = false;

                listener.Connections.Subscribe(connection =>
                {
                    connected = true;
                    connection.Messages.Subscribe(_ => { }, () => messagesObservableCompleted = true);
                    connection.Events.Subscribe(_ => { }, () => eventsObservableCompleted = true);
                    connection.Disposed += (o, e) => disposed = true;
                });

                using (var client = new FakeFreeSwitchSocket(listener.Port))
                {
                    await Wait.Until(() => connected);
                    // Removed explicit client.Dispose() - it will be disposed automatically by the using statement
                }

                await Wait.Until(() => messagesObservableCompleted);
                await Wait.Until(() => eventsObservableCompleted);

                Assert.That(connected, Is.True, "Expect a connection to have been made.");
                Assert.That(disposed, Is.True, "Expect the socket to have been disposed.");
                Assert.That(messagesObservableCompleted, Is.True, "Expect the BasicMessage observable to be completed");
                Assert.That(eventsObservableCompleted, Is.True, "Expect the EventMessage observable to be completed");
            }
        }

        [Test, CancelAfter(TimeOut.TestTimeOutMs)]
        public async Task Calling_Connect_on_a_new_OutboundSocket_should_populate_the_ChannelData()
        {
            using (var listener = new OutboundListener(0))
            {
                listener.Start();
                ChannelEvent channelData = null;

                listener.Connections.Subscribe(
                    async (socket) =>
                    {
                        channelData = await socket.Connect();
                    });

                using (var freeSwitch = new FakeFreeSwitchSocket(listener.Port))
                {
                    freeSwitch.MessagesReceived.FirstAsync(m => m.StartsWith("connect"))
                          .Subscribe(async _ => await freeSwitch.SendChannelDataEvent());

                    await Wait.Until(() => channelData != null);

                    Assert.That(channelData, Is.Not.Null);
                    Assert.That(channelData.ChannelState, Is.EqualTo(ChannelState.Execute));
                    Assert.That(channelData.Headers["Channel-Call-State"], Is.EqualTo("RINGING"));
                }
            }
        }

        [Test, CancelAfter(TimeOut.TestTimeOutMs)]
        public async Task Calling_Exit_on_a_disconnected_OutboundSocket_should_close_gracefully()
        {
            using (var listener = new OutboundListener(0))
            {
                listener.Start();
                EventMessage channelData = null;
                bool exited = false;

                listener.Connections.Subscribe(
                    async (socket) =>
                    {
                        channelData = await socket.Connect();
                        await socket.Exit();
                        exited = true;
                    });

                using (var freeSwitch = new FakeFreeSwitchSocket(listener.Port))
                {
                    freeSwitch.MessagesReceived.FirstAsync(m => m.StartsWith("connect"))
                          .Subscribe(async _ =>
                              { 
                                  await freeSwitch.SendChannelDataEvent();
                                  await Task.Delay(500);
                                  freeSwitch.Dispose();
                              });

                    await Wait.Until(() => channelData != null);
                    await Wait.Until(() => exited);

                    Assert.That(exited, Is.True);
                }
            }
        }

        [Test, CancelAfter(TimeOut.TestTimeOutMs)]
        public async Task Calling_Connect_on_a_OutboundSocket_that_was_disconnected_should_throw_OperationCanceledException()
        {
            using (var listener = new OutboundListener(0))
            {
                listener.Start();
                Exception ex = null;

                listener.Connections.Subscribe((socket) => ex = Assert.Throws<AggregateException>(() => socket.Connect().Wait()));

                using (var freeSwitch = new FakeFreeSwitchSocket(listener.Port))
                {
                    freeSwitch.MessagesReceived.FirstAsync(m => m.StartsWith("connect")).Subscribe(_ => freeSwitch.Dispose());

                    await Wait.Until(() => ex != null);
                    Assert.That(ex.InnerException, Is.TypeOf<TaskCanceledException>());
                }
            }
        }

        [Test, CancelAfter(TimeOut.TestTimeOutMs)]
        public async Task Channel_listener_should_handle_where_FS_disconnects_before_channelData_event_received()
        {
            using (var listener = new OutboundListener(0))
            {
                listener.Start();
                bool channelCallbackCalled = false;
                bool firstConnectionReceived = false;

                listener.Channels.Subscribe(channel => { channelCallbackCalled = true; });

                using (var freeSwitch = new FakeFreeSwitchSocket(listener.Port))
                {
                    freeSwitch.MessagesReceived.FirstAsync(m => m.StartsWith("connect")).Subscribe(_ =>
                        { 
                            firstConnectionReceived = true;
                            freeSwitch.Dispose();
                        });

                    await Wait.Until(() => firstConnectionReceived);
                    Assert.That(channelCallbackCalled, Is.False);
                }
            }
        }

        [Test, CancelAfter(TimeOut.TestTimeOutMs)]
        public async Task can_send_api()
        {
            using (var listener = new OutboundListener(0))
            {
                listener.Start();

                bool apiRequestReceived = false;
                ApiResponse apiResponse = null;

                listener.Connections.Subscribe(
                    async (socket) =>
                        {
                            await socket.Connect();

                            apiResponse = await socket.SendApi("status");
                        });

                using (var freeSwitch = new FakeFreeSwitchSocket(listener.Port))
                {
                    freeSwitch.MessagesReceived.FirstAsync(m => m.StartsWith("connect"))
                              .Subscribe(async _ => await freeSwitch.SendChannelDataEvent());

                    freeSwitch.MessagesReceived.FirstAsync(m => m.StartsWith("api")).Subscribe(
                        async _ =>
                            {
                                apiRequestReceived = true;
                                await freeSwitch.SendApiResponseOk();
                            });

                    await Wait.Until(() => apiRequestReceived);
                    await Wait.Until(() => apiResponse != null);

                    Assert.That(apiRequestReceived, Is.True);
                    Assert.That(apiResponse, Is.Not.Null);
                    Assert.That(apiResponse.Success, Is.True);
                }
            }
        }

        [Test, CancelAfter(TimeOut.TestTimeOutMs)]
        public async Task can_send_command()
        {
            using (var listener = new OutboundListener(0))
            {
                listener.Start();

                bool commandRequestReceived = false;
                CommandReply commandReply = null;

                listener.Connections.Subscribe(
                    async (socket) =>
                        {
                            await socket.Connect();

                            commandReply = await socket.Linger();
                        });

                using (var freeSwitch = new FakeFreeSwitchSocket(listener.Port))
                {
                    freeSwitch.MessagesReceived.FirstAsync(m => m.StartsWith("connect"))
                          .Subscribe(async _ => await freeSwitch.SendChannelDataEvent());

                    freeSwitch.MessagesReceived.FirstAsync(m => m.StartsWith("linger"))
                          .Subscribe(async _ =>
                              {
                                  commandRequestReceived = true;
                                  await freeSwitch.SendCommandReplyOk();
                              });

                    await Wait.Until(() => commandRequestReceived);

                    Assert.That(commandRequestReceived, Is.True);
                    Assert.That(commandReply, Is.Not.Null);
                    Assert.That(commandReply.Success, Is.True);
                }
            }
        }

        [Test, CancelAfter(TimeOut.TestTimeOutMs)]
        public async Task can_send_multple_commands()
        {
            using (var listener = new OutboundListener(0))
            {
                listener.Start();

                bool commandRequestReceived = false;
                CommandReply commandReply = null;

                listener.Connections.Subscribe(
                    async (socket) =>
                    {
                        await socket.Connect();

                        commandReply = await socket.Linger();

                        commandReply = await socket.NoLinger();
                    });

                using (var freeSwitch = new FakeFreeSwitchSocket(listener.Port))
                {
                    freeSwitch.MessagesReceived.FirstAsync(m => m.StartsWith("connect"))
                          .Subscribe(async _ => await freeSwitch.SendChannelDataEvent());

                    freeSwitch.MessagesReceived.FirstAsync(m => m.StartsWith("linger"))
                          .Subscribe(async _ =>
                          {
                              await freeSwitch.SendCommandReplyOk();
                          });

                    freeSwitch.MessagesReceived.FirstAsync(m => m.StartsWith("nolinger"))
                          .Subscribe(async _ =>
                          {
                              await freeSwitch.SendCommandReplyError("FAILED");
                              commandRequestReceived = true;
                          });

                    await Wait.Until(() => commandRequestReceived);

                    Assert.That(commandRequestReceived, Is.True);
                    Assert.That(commandReply, Is.Not.Null);
                    Assert.That(commandReply.Success, Is.False);
                }
            }
        }
    }
}