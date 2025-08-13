using System;

namespace Paramore.Brighter.Inbox.Exceptions
{
    public class RequestNotFoundException<T>(string id, Exception? innerException = null)
        : Exception($"Command '{id}' of type {typeof(T).FullName} does not exist", innerException)
        where T : IRequest;
}
