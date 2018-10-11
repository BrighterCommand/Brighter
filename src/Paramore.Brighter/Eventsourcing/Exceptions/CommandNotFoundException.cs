using System;

namespace Paramore.Brighter.Eventsourcing.Exceptions
{
    public class CommandNotFoundException<T> : Exception where T : IRequest
    {
        public CommandNotFoundException(Guid id)
            :this(id, null)
        {
        }

        public CommandNotFoundException(Guid id, Exception innerException)
            : base($"Command '{id}' of type {typeof(T).FullName} does not exist", innerException)
        {
        }
    }
}
