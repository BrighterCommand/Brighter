using System;

namespace Paramore.Brighter.Inbox.Exceptions
{
    public class RequestNotFoundException<T> : Exception where T : IRequest
    {
        public RequestNotFoundException(string id)
            :this(id, null)
        {
        }

        public RequestNotFoundException(string id, Exception innerException)
            : base($"Command '{id}' of type {typeof(T).FullName} does not exist", innerException)
        {
        }
    }
}
