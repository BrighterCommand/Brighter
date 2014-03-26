using System;

namespace paramore.brighter.commandprocessor
{
    public interface IAmAMessageStore<T> where T : Message
    {
        void Add(T message);
        Message Get(Guid messageId);
    }
}