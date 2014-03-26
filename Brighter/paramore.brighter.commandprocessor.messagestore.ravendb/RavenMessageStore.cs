using System;

namespace paramore.brighter.commandprocessor.messagestore.ravendb
{
    public class RavenMessageStore : IAmAMessageStore<Message>
    {
        public void Add(Message message)
        {
            throw new NotImplementedException();
        }

        public Message Get(Guid messageId)
        {
            throw new NotImplementedException();
        }
    }
}
