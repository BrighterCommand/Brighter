using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter;

namespace Tests
{
    public class TestEventMessageMapperAsync : IAmAMessageMapperAsync<TestEvent>
    {
        public IRequestContext Context { get; set; }

        public Task<Message> MapToMessageAsync(TestEvent request, Publication publication, CancellationToken ct = default)
        {
            throw new System.NotImplementedException();
        }

        public Task<TestEvent> MapToRequestAsync(Message message, CancellationToken ct = default)
        {
            throw new System.NotImplementedException();
        }
    }
}
