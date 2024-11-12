// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Parser.cs" company="Dan Barua">
//   (C) Dan Barua and contributors. Licensed under the Mozilla Public License.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------
namespace NEventSocket.Sockets
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using NEventSocket.FreeSwitch;
    using NEventSocket.Util;

    /// <summary>
    /// A parser for converting a stream of strings or chars into a stream of <seealso cref="BasicMessage"/>s from FreeSwitch.
    /// </summary>
    public class Parser : IDisposable
    {
        private byte previous;

        private int? contentLength;

        private IDictionary<string, string> headers;

        private MemoryStream headerBytes = new MemoryStream();
        private MemoryStream bodyBytes;

        private readonly InterlockedBoolean disposed = new InterlockedBoolean();

        private const byte headerEndDelimiter = (byte)'\n';
        ~Parser()
        {
            Dispose(false);
        }

        /// <summary>
        /// Gets a value indicating whether parsing an incoming message has completed.
        /// </summary>
        public bool Completed { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the incoming message has a body.
        /// </summary>
        public bool HasBody => contentLength.HasValue && contentLength > 0;

        /// <summary>
        /// Appends the given <see cref="byte"/> to the message.
        /// </summary>
        /// <param name="next">The next <see cref="byte"/> of the message.</param>
        /// <returns>The same instance of the <see cref="Parser"/>.</returns>
        public Parser Append(byte next)
        {
            if (Completed)
            {
                return new Parser().Append(next);
            }

            if (!HasBody)
            {
                headerBytes.WriteByte(next);
                // we're parsing the headers
                if (previous == headerEndDelimiter && next == headerEndDelimiter)
                {
                    // \n\n denotes the end of the Headers
                    var headerString = Encoding.UTF8.GetString(headerBytes.ToArray());

                    headers = headerString.ParseKeyValuePairs(": ");

                    if (headers.ContainsKey(HeaderNames.ContentLength))
                    {
                        contentLength = int.Parse(headers[HeaderNames.ContentLength]);

                        if (contentLength == 0)
                        {
                            Completed = true;
                        }
                        else
                        {
                            // start parsing the body content
                            bodyBytes = new MemoryStream(contentLength.Value);
                        }
                    }
                    else
                    {
                        // end of message
                        Completed = true;
                    }
                }
                else
                {
                    previous = next;
                }
            }
            else
            {
                Debug.Assert(bodyBytes != null);
                bodyBytes.WriteByte(next);
                // if we've read the Content-Length amount of bytes then we're done
                Debug.Assert(contentLength > 0);
                Completed = bodyBytes.Length == contentLength.GetValueOrDefault();
            }

            return this;
        }

        /// <summary>
        /// Appends the provided string to the internal buffer.
        /// </summary>
        public Parser Append(byte[] next)
        {
            var parser = this;

            foreach (var b in next)
            {
                parser = parser.Append(b);
            }

            return parser;
        }

        /// <summary>
        /// Extracts a <seealso cref="BasicMessage"/> from the internal buffer.
        /// </summary>
        /// <returns>A new <seealso cref="BasicMessage"/> instance.</returns>
        /// <exception cref="InvalidOperationException">
        /// When the parser has not received a complete message.
        /// Can be indicative of multiple threads attempting to read from the network stream.
        /// </exception>
        public BasicMessage ExtractMessage()
        {
            if (disposed.Value)
            {
                throw new ObjectDisposedException(GetType().Name, "Should only call ExtractMessage() once per parser.");
            }

            if (!Completed)
            {
                var errorMessage = "The message was not completely parsed.";

                if (HasBody)
                {
                    errorMessage += "expected a body with length {0}, got {1} instead.".Fmt(contentLength, bodyBytes.Length);
                }

                throw new InvalidOperationException(errorMessage);
            }

            var result = HasBody ? new BasicMessage(headers, bodyBytes.ToArray()) : new BasicMessage(headers);

            if (HasBody)
            {
                Debug.Assert(bodyBytes.Length == result.ContentLength);
            }

            Dispose();
            return result;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed != null && !disposed.EnsureCalledOnce())
            {
                bodyBytes = null;
            }
        }
    }
}