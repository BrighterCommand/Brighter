using System;

namespace Paramore.Brighter.Eventsourcing.Exceptions
{
    public class OnceOnlyException : Exception
    {
        public OnceOnlyException(){}

        public OnceOnlyException(string message) : base(message){}

        public OnceOnlyException(string message, Exception innerException) : base(message, innerException){}
    }
}