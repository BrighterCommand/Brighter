using System;

namespace Paramore.Brighter.Inbox.Exceptions
{
    public class RequestNotFoundException<T> : Exception where T : IRequest
    {
        public RequestNotFoundException(Guid id)
            :this(id, null)
        {
        }

        public RequestNotFoundException(Guid id, Exception innerException)
            : base($"Command '{id}' of type {typeof(T).FullName} does not exist", innerException)
        {
        }
    }
}
