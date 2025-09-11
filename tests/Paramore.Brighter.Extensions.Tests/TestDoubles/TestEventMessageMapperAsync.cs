using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Extensions.Tests.TestDoubles
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
