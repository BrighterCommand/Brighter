using System.Collections.Generic;

namespace paramore.brighter.commandprocessor
{
    public interface IAmAMessageRecoverer
    {
        void Repost(List<string> messageIds, IAmAMessageStore<Message> messageStore, IAmAMessageProducer messageProducer);
    }
}