using System;

namespace paramore.brighter.commandprocessor
{
    public interface IAmAMessageStore<T> where T : CommandMessage
    {
        void Add(T message);
        CommandMessage Get(Guid messageId);
    }
}