using System;

namespace paramore.brighter.restms.core.Ports.Common
{
    public class FeedAlreadyExistsException : Exception
    {
        public FeedAlreadyExistsException(){}

        public FeedAlreadyExistsException(string message):base(message){}

        public FeedAlreadyExistsException(string message, Exception innerException):base(message, innerException){}
    }
}
