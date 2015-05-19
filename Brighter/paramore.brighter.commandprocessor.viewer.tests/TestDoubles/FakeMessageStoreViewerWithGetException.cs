using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace paramore.brighter.commandprocessor.viewer.tests.TestDoubles
{
    internal class FakeMessageStoreViewerWithGetException : IAmAMessageStore<Message>, IAmAMessageStoreViewer<Message>
    {
        public Task Add(Message message)
        {
            return Task.FromResult((object) null);

        }

        public Task<Message> Get(Guid messageId)
        {
            throw new AggregateException();
        }

        public Task<IList<Message>> Get(int pageSize = 100, int pageNumber = 1)
        {
            throw new AggregateException();
        }
    }
}