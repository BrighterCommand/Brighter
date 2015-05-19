using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace paramore.brighter.commandprocessor.viewer.tests.TestDoubles
{
    internal class FakeMessageStoreWithViewer : IAmAMessageStoreViewer<Message>, IAmAMessageStore<Message>
    {
        private readonly IList<Message> messages = new List<Message>();
        public Task<IList<Message>> Get(int pageSize = 100, int pageNumber = 1)
        {
            return Task.FromResult(messages);
        }

        public Task Add(Message message)
        {
            messages.Add(message);
            return Task.FromResult((object)null);
        }

        public Task<Message> Get(Guid messageId)
        {
            return Task.FromResult(messages.SingleOrDefault(m => m.Id == messageId));
        }
    }
}