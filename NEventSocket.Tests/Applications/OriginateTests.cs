﻿namespace NEventSocket.Tests.Applications
{
    using System.Collections.Generic;
    using System.Text.Json;

    using NEventSocket.FreeSwitch;

    using NUnit.Framework;

    [TestFixture]
    public class OriginateTests
    {
        [Test]
        public void can_format_originate_options()
        {
            var options = new OriginateOptions()
                              {
                                  CallerIdName = "Dan",
                                  CallerIdNumber = "0123457890",
                                  ExecuteOnOriginate = "my_app::my_arg",
                                  Retries = 5,
                                  RetrySleepMs = 200,
                                  ReturnRingReady = true,
                                  TimeoutSeconds = 60,
                                  UUID = "83fe4f3d-b957-4b26-b6bf-3879d7e21972",
                                  IgnoreEarlyMedia = true,
                              };

            Assert.That(options.ToString(), Is.EqualTo(
                "{origination_caller_id_name='Dan',origination_caller_id_number='0123457890',execute_on_originate='my_app::my_arg',originate_retries='5',originate_retry_sleep_ms='200',return_ring_ready='true',originate_timeout='60',origination_uuid='83fe4f3d-b957-4b26-b6bf-3879d7e21972',ignore_early_media='true'}"));
        }

        [Test]
        public void can_set_enterprise_channel_variables()
        {
            var options = new OriginateOptions
                {
                    EnterpriseChannelVariables = new Dictionary<string, string>
                    {
                         {"e1" , "ev1"},
                         {"e2" , "ev2"}
                    }
                }.ToString();
            Assert.That(options, Does.Contain("<e1='ev1',e2='ev2'>"));
        }

        [Test]
        public void can_set_enterprise_channel_variables_and_channel_variables()
        {
            var options = new OriginateOptions
                          {
                              EnterpriseChannelVariables = new Dictionary<string, string> { { "e1", "ev1" }, { "e2", "ev2" } },
                              ChannelVariables = new Dictionary<string, string> { { "c1", "cv1" }, { "c2", "cv2" } }
                          }.ToString();
            Assert.That(options, Does.Contain("<e1='ev1',e2='ev2'>{c1='cv1',c2='cv2'}"));
        }

        [Test]
        public void can_set_caller_id_type()
        {
            var options = new OriginateOptions() { SipCallerIdType = SipCallerIdType.RPid }.ToString();
            Assert.That(options, Does.Contain("sip_cid_type='rpid'"));
        }

        [Test]
        public void can_set_privacy()
        {
            var options = new OriginateOptions() { OriginationPrivacy = OriginationPrivacy.HideName | OriginationPrivacy.HideNumber | OriginationPrivacy.Screen}.ToString();
            Assert.That(options, Does.Contain("origination_privacy='hide_name:hide_number:screen'"));
        }

        [Test]
        public void can_serialize_and_deserialize_OriginateOptions()
        {
                var options = new OriginateOptions()
            {
                CallerIdName = "Dan",
                CallerIdNumber = "0123457890",
                ExecuteOnOriginate = "my_app::my_arg",
                Retries = 5,
                RetrySleepMs = 200,
                ReturnRingReady = true,
                TimeoutSeconds = 60,
                UUID = "83fe4f3d-b957-4b26-b6bf-3879d7e21972",
                IgnoreEarlyMedia = true,
            };

            var json = JsonSerializer.Serialize(options);
            var fromJson = JsonSerializer.Deserialize<OriginateOptions>(json);

            Assert.That(fromJson.ChannelVariables, Is.EquivalentTo(options.ChannelVariables));
        }
    }
}