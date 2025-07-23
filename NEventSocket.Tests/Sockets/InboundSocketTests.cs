namespace NEventSocket.Tests.Sockets
{
    using System;
    using System.Reactive.Linq;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Logging;

    using NUnit.Framework;

    using NEventSocket;
    using NEventSocket.Logging;
    using NEventSocket.Tests.Fakes;
    using NEventSocket.Tests.TestSupport;

    [TestFixture]
    public class InboundSocketTests
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
        public async Task sending_a_correct_password_should_connect()
        {
            using (var listener = new FakeFreeSwitchListener(0))
            {
                listener.Start();

                bool authRequestReceived = false;
                bool exitRequestReceived = false;

                listener.Connections.Subscribe(
                    async socket =>
                    {
                        socket.MessagesReceived.Where(m => m.Equals("auth ClueCon"))
                              .Subscribe(async m =>
                              {
                                  authRequestReceived = true;
                                      await socket.SendCommandReplyOk();
                                  });

                        socket.MessagesReceived.Where(m => m.Equals("exit"))
                              .Subscribe(
                                  async _ =>
                                      {
                                          exitRequestReceived = true;
                                          await socket.SendCommandReplyOk();
                                          await socket.SendDisconnectNotice();
                                      });
                        
                        await socket.Send("Content-Type: auth/request");
                    });

                using (var client = await InboundSocket.Connect("127.0.0.1", listener.Port, "ClueCon"))
                {
                    Assert.That(authRequestReceived, Is.True);

                    await client.Exit();

                    await Wait.Until(() => exitRequestReceived);
                    Assert.That(exitRequestReceived, Is.True);
                }
            }
        }

        [Test, CancelAfter(TimeOut.TestTimeOutMs)]
        public void an_invalid_password_should_throw_an_InboundSocketConnectionFailedException()
        {
            using (var listener = new FakeFreeSwitchListener(0))
            {
                listener.Start();

                bool authRequestReceived = false;

                listener.Connections.Subscribe(
                    async socket =>
                    {
                        socket.MessagesReceived.Where(m => m.StartsWith("auth"))
                          .Subscribe(async m =>
                          {
                              authRequestReceived = true;
                              await socket.SendCommandReplyError("Invalid Password");
                          });

                        await socket.Send("Content-Type: auth/request");
                    });

                var aggregateException = Assert.Throws<AggregateException>(() => InboundSocket.Connect("127.0.0.1", listener.Port, "WrongPassword").Wait());
                Assert.That(authRequestReceived, Is.True);
                Assert.That(aggregateException.InnerException, Is.TypeOf<InboundSocketConnectionFailedException>());
                Assert.That(aggregateException.InnerException.Message, Is.EqualTo("Invalid password when trying to connect to 127.0.0.1:" + listener.Port));
            }
        }

        [Test, CancelAfter(TimeOut.TestTimeOutMs)]
        public void when_no_AuthRequest_received_it_should_throw_TimeoutException_wrapped_in_InboundSocketConnectionFailedException()
        {
            using (var listener = new FakeFreeSwitchListener(0))
            {
                listener.Start();

                var aggregateException = Assert.Throws<AggregateException>(() => InboundSocket.Connect("127.0.0.1", listener.Port, "ClueCon", TimeSpan.FromMilliseconds(100)).Wait());
                Assert.That(aggregateException.InnerException, Is.TypeOf<InboundSocketConnectionFailedException>());
                Assert.That(aggregateException.InnerException.InnerException, Is.TypeOf<TimeoutException>());
            }
        }

        [Test, CancelAfter(5000)]
        public void when_no_response_to_auth_received_it_should_throw_TimeoutException_wrapped_in_InboundSocketConnectionFailedException()
        {
            using (var listener = new FakeFreeSwitchListener(0))
            {
                listener.Start();

                listener.Connections.Subscribe(
                    async socket =>
                    {
                        await socket.Send("Content-Type: auth/request");
                    });

                var aggregateException = Assert.Throws<AggregateException>(() => InboundSocket.Connect("127.0.0.1", listener.Port, "ClueCon", TimeSpan.FromMilliseconds(100)).Wait());
                Assert.That(aggregateException.InnerException, Is.TypeOf<InboundSocketConnectionFailedException>());
                Assert.That(aggregateException.InnerException.InnerException, Is.TypeOf<TimeoutException>());
            }
        }

        [Test, CancelAfter(TimeOut.TestTimeOutMs)]
        public async Task can_send_api()
        {
            using (var listener = new FakeFreeSwitchListener(0))
            {
                listener.Start();

                listener.Connections.Subscribe(
                    async socket =>
                    {
                        socket.MessagesReceived.Where(m => m.Equals("auth ClueCon"))
                              .Take(1)
                              .Subscribe(async m =>
                              {
                                  await socket.SendCommandReplyOk();
                              });

                        socket.MessagesReceived.Where(m => m.StartsWith("api"))
                              .Take(1)
                              .Subscribe(
                                  async m =>
                                  {
                                      await socket.SendApiResponseOk();
                                  });

                        await socket.Send("Content-Type: auth/request");
                    });

                using (var client = await InboundSocket.Connect("127.0.0.1", listener.Port, "ClueCon"))
                {
                    var result = await client.SendApi("status");
                    Assert.That(result.Success, Is.True);
                }
            }
        }

        [Test, CancelAfter(5000)]
        public async Task when_no_api_response_received_it_should_throw_a_TimeOutException()
        {
            using (var listener = new FakeFreeSwitchListener(0))
            {
                listener.Start();

                bool apiRequestReceived = false;

                listener.Connections.Subscribe(
                    async socket =>
                    {
                        socket.MessagesReceived.FirstAsync(m => m.Equals("auth ClueCon"))
                              .Subscribe(async m =>
                              {
                                  await socket.SendCommandReplyOk();
                              });

                        socket.MessagesReceived.FirstAsync(m => m.StartsWith("api"))
                              .Subscribe(async m =>
                                  {
                                      apiRequestReceived = true;
                                      await Task.Delay(1000);
                                      await socket.SendApiResponseError("error");
                                  });

                        socket.MessagesReceived.Where(m => m.Equals("exit"))
                              .Subscribe(
                                  async _ =>
                                  {
                                      await socket.SendCommandReplyOk();
                                      await socket.SendDisconnectNotice();
                                  });

                        await socket.Send("Content-Type: auth/request");
                    });

                using (var client = await InboundSocket.Connect("127.0.0.1", listener.Port, "ClueCon", TimeSpan.FromMilliseconds(100)))
                {
                    client.ResponseTimeOut = TimeSpan.FromMilliseconds(100);
                    var ex = Assert.Throws<AggregateException>(() => client.SendApi("status").Wait());

                    Assert.That(ex, Is.Not.Null);
                    Assert.That(ex.InnerException, Is.TypeOf<TimeoutException>());
                    Assert.That(apiRequestReceived, Is.True);

                    await client.Exit();
                }
            }
        }

        [Test, CancelAfter(TimeOut.TestTimeOutMs)]
        public async Task can_send_command()
        {
            using (var listener = new FakeFreeSwitchListener(0))
            {
                listener.Start();

                listener.Connections.Subscribe(
                    async socket =>
                    {
                        socket.MessagesReceived.Where(m => m.Equals("auth ClueCon"))
                              .Take(1)
                              .Subscribe(async m =>
                              {
                                  await socket.SendCommandReplyOk();
                              });

                        socket.MessagesReceived.Where(m => m.StartsWith("test"))
                              .Take(1)
                              .Subscribe(
                                  async m =>
                                  {
                                      await socket.SendCommandReplyOk();
                                  });

                        await socket.Send("Content-Type: auth/request");
                    });

                using (var client = await InboundSocket.Connect("127.0.0.1", listener.Port, "ClueCon"))
                {
                    var result = await client.SendCommand("test");
                    Assert.That(result.Success, Is.True);
                }
            }
        }

        [Test, CancelAfter(TimeOut.TestTimeOutMs)]
        public async Task can_send_multiple_commands()
        {
            using (var listener = new FakeFreeSwitchListener(0))
            {
                listener.Start();

                listener.Connections.Subscribe(
                    async socket =>
                    {
                        socket.MessagesReceived.Where(m => m.Equals("auth ClueCon"))
                              .Take(1)
                              .Subscribe(async m =>
                              {
                                  await socket.SendCommandReplyOk();
                              });

                        socket.MessagesReceived.Where(m => m.StartsWith("test"))
                              .Take(1)
                              .Subscribe(
                                  async m =>
                                  {
                                      await socket.SendCommandReplyOk();
                                  });

                       socket.MessagesReceived.FirstAsync(m => m.StartsWith("event"))
                              .Subscribe(
                                  async m =>
                                  {
                                      await socket.SendCommandReplyError("FAILED");
                                  });

                        await socket.Send("Content-Type: auth/request");
                    });

                using (var client = await InboundSocket.Connect("127.0.0.1", listener.Port, "ClueCon"))
                {
                    var result = await client.SendCommand("test");
                    Assert.That(result.Success, Is.True);

                    result = await client.SendCommand("event CHANNEL_ANSWER");
                    Assert.That(result.Success, Is.False);
                }
            }
        }

        [Test, CancelAfter(5000)]
            public async Task when_no_command_reply_received_it_should_throw_a_TimeOutException()
        {
            using (var listener = new FakeFreeSwitchListener(0))
            {
                listener.Start();

                bool commandRequestReceived = false;

                listener.Connections.Subscribe(
                    async socket =>
                    {
                        socket.MessagesReceived.FirstAsync(m => m.Equals("auth ClueCon"))
                              .Subscribe(async m =>
                              {
                                  await socket.SendCommandReplyOk();
                              });

                        socket.MessagesReceived.FirstAsync(m => m.StartsWith("test"))
                              .Subscribe(async m =>
                              {
                                  commandRequestReceived = true;
                                  await Task.Delay(1000);
                                  await socket.SendCommandReplyError("error");
                              });

                        await socket.Send("Content-Type: auth/request");
                    });

                using (var client = await InboundSocket.Connect("127.0.0.1", listener.Port, "ClueCon", TimeSpan.FromMilliseconds(100)))
                {
                    var ex = Assert.Throws<AggregateException>(() => client.SendCommand("test").Wait());
                    Assert.That(ex, Is.Not.Null);
                    Assert.That(ex.InnerException, Is.TypeOf<TimeoutException>());
                    Assert.That(commandRequestReceived, Is.True);
                }
            }
        }

        [Test, CancelAfter(TimeOut.TestTimeOutMs)]
        public async Task when_the_inbound_socket_is_disposed_it_should_complete_the_observables()
        {
            using (var listener = new FakeFreeSwitchListener(0))
            {
                listener.Start();

                listener.Connections.Subscribe(
                    async socket =>
                    {
                        socket.MessagesReceived.Where(m => m.Equals("auth ClueCon"))
                              .Take(1)
                              .Subscribe(async m =>
                              {
                                  await socket.SendCommandReplyOk();
                              });


                        socket.MessagesReceived.Where(m => m.StartsWith("exit"))
                              .Take(1)
                              .Subscribe(
                                  async m =>
                                  {
                                      await socket.SendCommandReplyOk();
                                      await socket.SendDisconnectNotice();
                                  });

                        await socket.Send("Content-Type: auth/request");
                    });

                using (var client = await InboundSocket.Connect("127.0.0.1", listener.Port, "ClueCon"))
                {
                    bool completed = false;
                    
                    client.Messages.Subscribe(_ => { },ex => { },() => completed = true);

                    await client.Exit();
                    client.Dispose();

                    await Wait.Until(() => completed);
                    Assert.That(completed, Is.True);
                }
            }
        }

        [Test, CancelAfter(TimeOut.TestTimeOutMs)]
        public async Task when_FreeSwitch_disconnects_it_should_complete_the_observables()
        {
            using (var listener = new FakeFreeSwitchListener(0))
            {
                listener.Start();

                bool disconnected = false;

                listener.Connections.Subscribe(
                    async socket =>
                    {
                        socket.MessagesReceived.Where(m => m.Equals("auth ClueCon"))
                              .Take(1)
                              .Subscribe(async m =>
                              {
                                  await socket.SendCommandReplyOk();
                              });


                        socket.MessagesReceived.Where(m => m.StartsWith("exit"))
                              .Take(1)
                              .Subscribe(
                                  async m =>
                                  {
                                      await socket.SendCommandReplyOk();
                                      await socket.SendDisconnectNotice();
                                      socket.Dispose();
                                      disconnected = true;
                                  });

                        await socket.Send("Content-Type: auth/request");
                    });

                using (var client = await InboundSocket.Connect("127.0.0.1", listener.Port, "ClueCon"))
                {
                    bool completed = false;
                    client.Messages.Subscribe(_ => { }, ex => { }, () => completed = true);

                    await client.Exit();

                    await Wait.Until(() => disconnected);
                    Console.WriteLine("Disconnected, completed:" + completed);

                    await Wait.Until(() => completed);

                    Assert.That(completed, Is.True);
                }
            }
        }

        [Test, CancelAfter(TimeOut.TestTimeOutMs)]
        public async Task when_a_command_reply_error_is_received_in_response_to_an_application_request_it_should_return_a_failed_ApplicationResult()
        {
            using (var listener = new FakeFreeSwitchListener(0))
            {
                listener.Start();

                listener.Connections.Subscribe(
                    async socket =>
                    {
                        socket.MessagesReceived.Where(m => m.Equals("auth ClueCon"))
                              .Take(1)
                              .Subscribe(async m =>
                              {
                                  await socket.SendCommandReplyOk();
                              });

                        socket.MessagesReceived.Where(m => m.StartsWith("event"))
                              .Take(1)
                              .Subscribe(async m => await socket.SendCommandReplyOk());

                        socket.MessagesReceived.Where(m => m.StartsWith("sendmsg"))
                              .Take(1)
                              .Subscribe(
                                  async m =>
                                  {
                                      await socket.SendCommandReplyError("invalid session id [c1cdaeae-ebb0-4f3f-8f75-0f673bfbc046]");
                                  });

                        await socket.Send("Content-Type: auth/request");
                    });

                using (var client = await InboundSocket.Connect("127.0.0.1", listener.Port, "ClueCon"))
                {
                    var result = await client.Play("c1cdaeae-ebb0-4f3f-8f75-0f673bfbc046", "test.wav");
                    Assert.That(result.Success, Is.False);
                }
            }
        }

        [Test, CancelAfter(TimeOut.TestTimeOutMs)]
        public async Task when_a_CHANNEL_EXECUTE_COMPLETE_event_is_returned_it_should_complete_the_Application()
        {
            using (var listener = new FakeFreeSwitchListener(0))
            {
                listener.Start();

                listener.Connections.Subscribe(
                    async socket =>
                    {
                        socket.MessagesReceived.Where(m => m.Equals("auth ClueCon"))
                              .Take(1)
                              .Subscribe(async m =>
                              {
                                  await socket.SendCommandReplyOk();
                              });

                        socket.MessagesReceived.Where(m => m.StartsWith("event"))
                              .Take(1)
                              .Subscribe(async m => await socket.SendCommandReplyOk());

                        socket.MessagesReceived.Where(m => m.StartsWith("sendmsg"))
                              .Take(1)
                              .Subscribe(
                                  async m =>
                                      {
                                          var regex = new Regex(@"sendmsg (?<channelUUID>\S+)\nEvent-UUID: (?<applicationUUID>\S+)\n");
                                          var matches = regex.Match(m);

                                          var channelUUID = matches.Groups["channelUUID"].Value;
                                          var applicationUUID = matches.Groups["applicationUUID"].Value;

                                          var channelExecuteComplete =
                                              TestMessages.PlaybackComplete
                                              .Replace("Application-UUID: fd3ababd-ad60-4582-8c6c-609064d55fe7", "Application-UUID: " + applicationUUID)
                                              .Replace("Unique-ID: 4e1cfa50-4c2f-44c9-aaf3-8ca590bed0e4", "Unique-ID: " + channelUUID)
                                              .Replace("\r\n", "\n");

                                          await socket.SendCommandReplyOk();
                                          await socket.Send(channelExecuteComplete);
                                        });

                        await socket.Send("Content-Type: auth/request");
                    });

                using (var client = await InboundSocket.Connect("127.0.0.1", listener.Port, "ClueCon"))
                {
                    var result = await client.Play("4e1cfa50-4c2f-44c9-aaf3-8ca590bed0e4", "test.wav");
                    Assert.That(result.Success, Is.True);
                    Assert.That(result.ResponseText, Is.EqualTo("FILE PLAYED"));
                }
            }
        }
    }
}