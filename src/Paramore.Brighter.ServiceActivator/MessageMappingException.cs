using System;

namespace Paramore.Brighter.ServiceActivator
{
    internal class MessageMappingException : Exception
    {
        public MessageMappingException(string message, Exception exception) : base(message, exception) { }
    }
}
