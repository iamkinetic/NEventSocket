using System;

namespace NEventSocket
{
    public class MessageReceiverException : Exception
    {
        public MessageReceiverException(string message) : base(message)
        {
        }

        public MessageReceiverException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
