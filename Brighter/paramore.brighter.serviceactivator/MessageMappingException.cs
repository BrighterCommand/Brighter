using System;

namespace paramore.brighter.serviceactivator
{
    internal class MessageMappingException : Exception
    {
        public MessageMappingException(string message, Exception exception) : base(message, exception) { }
    }
}
