using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles
{
    internal class FailingEventMessageMapperAsync : IAmAMessageMapperAsync<MyFailingMapperEvent>
    {
        public Task<Message> MapToMessage(MyFailingMapperEvent request)
        {
            var tcs = new TaskCompletionSource<Message>();
            tcs.SetException(new JsonException());
            return tcs.Task;
        }

        public Task<MyFailingMapperEvent> MapToRequest(Message message)
        {
            var tcs = new TaskCompletionSource<MyFailingMapperEvent>();
            tcs.SetException(new JsonException());
            return tcs.Task;
        }
    }
}
