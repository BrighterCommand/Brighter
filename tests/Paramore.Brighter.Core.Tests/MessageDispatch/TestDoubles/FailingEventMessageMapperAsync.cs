using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles
{
    internal class FailingEventMessageMapperAsync : IAmAMessageMapperAsync<MyFailingMapperEvent>
    {
        public Task<Message> MapToMessageAsync(MyFailingMapperEvent request, Publication publication, CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<Message>();
            tcs.SetException(new JsonException());
            return tcs.Task;
        }

        public Task<MyFailingMapperEvent> MapToRequestAsync(Message message, CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<MyFailingMapperEvent>();
            tcs.SetException(new JsonException());
            return tcs.Task;
        }
    }
}
