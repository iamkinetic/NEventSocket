﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NEventSocket
{
    using NEventSocket.Util;

    public static class Digits
    {
        public static string ToFileString(this int count, bool ignoreZeros = false)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException("count must be greater than zero");
            }

            if (count >= 1000000)
            {
                if (count % 1000000 == 0)
                {
                    return string.Format("{0}!digits/million.wav", Digits.ToFileString(Math.Abs(count / 1000000)));
                }

                return string.Format("{0}!digits/million.wav!{1}", Digits.ToFileString(Math.Abs(count / 1000000)), Digits.ToFileString(count % 1000000, true));
            }

            if (count >= 1000 && count < 1000000)
            {
                if (count % 1000 == 0)
                {
                    return string.Format("digits/{0}.wav!digits/thousand.wav", count / 1000);
                }

                return string.Format("{0}!digits/thousand.wav!{1}", Digits.ToFileString(Math.Abs(count / 1000)), Digits.ToFileString(count % 1000, true));
            }

            if (count >= 100 && count < 1000)
            {
                if (count % 100 == 0)
                {
                    return string.Format("digits/{0}.wav!digits/hundred.wav", count / 100);
                }

                return string.Format("digits/{0}.wav!digits/hundred.wav!{1}", Math.Abs(count / 100), Digits.ToFileString(count % 100, true));
            }

            if (count > 20 && count <= 99)
            {
                return string.Format("digits/{0}.wav!digits/{1}.wav", Math.Abs(count / 10) * 10, count % 10);
            }

            if (count > 0 && count <= 20)
            {
                return string.Format("digits/{0}.wav", count);
            }

            if (count == 0 && !ignoreZeros)
            {
                return "digits/0.wav";
            }

            return string.Empty;
        }
    }
}
