using System;

namespace paramore.brighter.restms.core.Ports.Common
{
    public class FeedDoesNotExistException : Exception
    {
        public FeedDoesNotExistException() {}

        public FeedDoesNotExistException(string message) : base(message){}

        public FeedDoesNotExistException(string message, Exception innerException) : base(message, innerException){}
    }
}
