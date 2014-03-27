using System;
using Raven.Client;

namespace paramore.brighter.commandprocessor.messagestore.ravendb
{
    public class RavenMessageStore : IAmAMessageStore<Message>
    {
        private readonly IDocumentStore documentStore;

        public RavenMessageStore(IDocumentStore documentStore)
        {
            this.documentStore = documentStore;
        }

        public void Add(Message message)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                session.StoreAsync(message);
            }
        }

        public Message Get(Guid messageId)
        {
            using (var session = documentStore.OpenSession())
            {
                return session.Load<Message>(messageId);
            }
        }
    }
}
