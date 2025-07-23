using System;
using FluentAssertions;
using NEventSocket;
using NUnit.Framework;
using NEventSocket.FreeSwitch;
using NEventSocket.Util;

namespace NEventSocket.Tests.Util
{
    [TestFixture]
    public class StringExtensionsTests
    {
        [Test]
        public void can_format_strings()
        {
            const string Format = "{0} {1} {2}";
            var output = Format.Fmt(1, 2, 3);
            Assert.That(output, Is.EqualTo("1 2 3"));
        }

        [Test]
        public void can_convert_camelcase_to_uppercaseunderscore()
        {
            const string Input = "ThisIsAStringInCamelCase";
            Assert.That(Input.ToUpperWithUnderscores(), Is.EqualTo("THIS_IS_A_STRING_IN_CAMEL_CASE"));
        }

        [Test]
        public void can_convert_uppercaseunderscore_to_camelcase()
        {
            const string Input = "THIS_IS_A_STRING_IN_UPPER_CASE";
            Assert.That(Input.ToPascalCase(), Is.EqualTo("ThisIsAStringInUpperCase"));
        }

        [Test]
        public void can_convert_uppercaseunderscore_to_enum()
        {
            const string Input = "UNALLOCATED_NUMBER";
            var output = Input.HeaderToEnum<HangupCause>();

            Assert.That(output, Is.EqualTo(HangupCause.UnallocatedNumber));
        }

        [Test]
        public void if_unable_to_convert_string_to_nullable_enum_it_should_return_null()
        {
            const string Input = "THIS_IS_AN_INVALID_HANGUPCAUSE";
            var output = Input.HeaderToEnumOrNull<HangupCause>();

            Assert.That(output, Is.Null);
        }

        [Test]
        public void if_unable_to_convert_string_to_enum_it_should_throw_an_ArgumentException()
        {
            const string Input = "THIS_IS_AN_INVALID_HANGUPCAUSE";
            Assert.Throws<ArgumentException>(() => Input.HeaderToEnum<HangupCause>());
        }

        [Test]
        [TestCase(0, "digits/0.wav")]
        [TestCase(1, "digits/1.wav")]
        [TestCase(2, "digits/2.wav")]
        [TestCase(10, "digits/10.wav")]
        [TestCase(11, "digits/11.wav")]
        [TestCase(12, "digits/12.wav")]
        [TestCase(20, "digits/20.wav")]
        [TestCase(23, "digits/20.wav!digits/3.wav")]
        [TestCase(36, "digits/30.wav!digits/6.wav")]
        [TestCase(100, "digits/1.wav!digits/hundred.wav")]
        [TestCase(110, "digits/1.wav!digits/hundred.wav!digits/10.wav")]
        [TestCase(116, "digits/1.wav!digits/hundred.wav!digits/16.wav")]
        [TestCase(123, "digits/1.wav!digits/hundred.wav!digits/20.wav!digits/3.wav")]
        [TestCase(199, "digits/1.wav!digits/hundred.wav!digits/90.wav!digits/9.wav")]
        [TestCase(1000, "digits/1.wav!digits/thousand.wav")]
        [TestCase(1005, "digits/1.wav!digits/thousand.wav!digits/5.wav")]
        [TestCase(1010, "digits/1.wav!digits/thousand.wav!digits/10.wav")]
        [TestCase(1016, "digits/1.wav!digits/thousand.wav!digits/16.wav")]
        [TestCase(1023, "digits/1.wav!digits/thousand.wav!digits/20.wav!digits/3.wav")]
        [TestCase(1099, "digits/1.wav!digits/thousand.wav!digits/90.wav!digits/9.wav")]
        [TestCase(1200, "digits/1.wav!digits/thousand.wav!digits/2.wav!digits/hundred.wav")]
        [TestCase(1305, "digits/1.wav!digits/thousand.wav!digits/3.wav!digits/hundred.wav!digits/5.wav")]
        [TestCase(1310, "digits/1.wav!digits/thousand.wav!digits/3.wav!digits/hundred.wav!digits/10.wav")]
        [TestCase(2316, "digits/2.wav!digits/thousand.wav!digits/3.wav!digits/hundred.wav!digits/16.wav")]
        [TestCase(2323, "digits/2.wav!digits/thousand.wav!digits/3.wav!digits/hundred.wav!digits/20.wav!digits/3.wav")]
        [TestCase(2399, "digits/2.wav!digits/thousand.wav!digits/3.wav!digits/hundred.wav!digits/90.wav!digits/9.wav")]
        [TestCase(20009, "digits/20.wav!digits/thousand.wav!digits/9.wav")]
        [TestCase(21239, "digits/20.wav!digits/1.wav!digits/thousand.wav!digits/2.wav!digits/hundred.wav!digits/30.wav!digits/9.wav")]
        [TestCase(123456, "digits/1.wav!digits/hundred.wav!digits/20.wav!digits/3.wav!digits/thousand.wav!digits/4.wav!digits/hundred.wav!digits/50.wav!digits/6.wav")]
        [TestCase(999999, "digits/9.wav!digits/hundred.wav!digits/90.wav!digits/9.wav!digits/thousand.wav!digits/9.wav!digits/hundred.wav!digits/90.wav!digits/9.wav")]
        [TestCase(2123456, "digits/2.wav!digits/million.wav!digits/1.wav!digits/hundred.wav!digits/20.wav!digits/3.wav!digits/thousand.wav!digits/4.wav!digits/hundred.wav!digits/50.wav!digits/6.wav")]
        [TestCase(9999999, "digits/9.wav!digits/million.wav!digits/9.wav!digits/hundred.wav!digits/90.wav!digits/9.wav!digits/thousand.wav!digits/9.wav!digits/hundred.wav!digits/90.wav!digits/9.wav")]
        [TestCase(1000023, "digits/1.wav!digits/million.wav!digits/20.wav!digits/3.wav")]
        [TestCase(123000000, "digits/1.wav!digits/hundred.wav!digits/20.wav!digits/3.wav!digits/million.wav")]
        public void can_convert_digits_to_file_strings(int input, string expectedOutput)
        {
            var output = Digits.ToFileString(input);
            output.Should().Be(
                expectedOutput, 
                "\nexpected: '{0}'\nactual: '{1}'\n".Fmt(
                    expectedOutput.Replace("digits/","").Replace(".wav","")
                    ,output.Replace("digits/", "").Replace(".wav", "")));
        }
    }
}