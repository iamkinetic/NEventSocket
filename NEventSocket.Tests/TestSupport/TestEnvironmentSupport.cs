using System;
using NUnit.Framework;

namespace NEventSocket.Tests.TestSupport
{   
    [TestFixture]
    public class TestEnvironmentSupport
    {
        static TestEnvironmentSupport()
        {
            //issues logging to stdout in AppVeyor and Travis environments, best to turn it off
            if (Environment.GetEnvironmentVariable("APPVEYOR_BUILD_NUMBER") == null && Environment.GetEnvironmentVariable("TRAVIS") == null)
            {
                //LogProvider.SetCurrentLogProvider(new ColouredConsoleLogProvider(LogLevel.Trace));
            }
        }

        [Test]
        public void EmptyTest()
        {
        }
    }
}
