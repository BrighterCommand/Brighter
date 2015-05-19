using System;
using System.Threading.Tasks;

namespace paramore.brighter.commandprocessor.viewer.tests.TestDoubles
{
    internal class FakeMessageStoreNotViewer : IAmAMessageStore<Message>
    {
        public Task Add(Message message)
        {
            return Task.FromResult((object) null);
        }
        public Task<Message> Get(Guid messageId)
        {
            return Task.FromResult((Message) null);
        }
    }
}