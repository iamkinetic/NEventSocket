namespace NEventSocket.Tests.Applications
{
    using System.Text.Json;

    using NEventSocket.FreeSwitch;

    using NUnit.Framework;

    [TestFixture]
    public class BridgeTests
    {
        [Test]
        public void can_format_BridgeOptions()
        {
            var options = new BridgeOptions()
            {
                UUID = "985cea12-4e70-4c03-8a2c-2c4b4502bbbb",
                TimeoutSeconds = 20,
                CallerIdName = "Dan B Leg",
                CallerIdNumber = "987654321",
                HangupAfterBridge = false,
                IgnoreEarlyMedia = true,
                ContinueOnFail = true,
                RingBack = "${uk-ring}"
            };

            // todo: allow exporting variables?
            options.ChannelVariables.Add("foo", "bar");
            options.ChannelVariables.Add("baz", "widgets");

            var toString = options.ToString();
            const string Expected = "{origination_uuid='985cea12-4e70-4c03-8a2c-2c4b4502bbbb',leg_timeout='20',origination_caller_id_name='Dan B Leg',origination_caller_id_number='987654321',ignore_early_media='true'}";
            Assert.That(toString, Is.EqualTo(Expected));
        }

        [Test]
        public void can_serialize_and_deserialize_BridgeOptions()
        {
            var options = new BridgeOptions()
            {
                UUID = "985cea12-4e70-4c03-8a2c-2c4b4502bbbb",
                TimeoutSeconds = 20,
                CallerIdName = "Dan B Leg",
                CallerIdNumber = "987654321",
                HangupAfterBridge = false,
                IgnoreEarlyMedia = true,
                ContinueOnFail = true,
                RingBack = "${uk-ring}"
            };

            options.ChannelVariables.Add("foo", "bar");
            options.ChannelVariables.Add("baz", "widgets");

            var json = JsonSerializer.Serialize(options);
            
            var fromJson = JsonSerializer.Deserialize<BridgeOptions>(json);
            
            Assert.That(fromJson, Is.EqualTo(options));
        }
    }
}