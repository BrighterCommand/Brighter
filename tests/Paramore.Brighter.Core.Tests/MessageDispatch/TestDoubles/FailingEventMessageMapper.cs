using Newtonsoft.Json;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles
{
    internal sealed class FailingEventMessageMapper : IAmAMessageMapper<MyFailingMapperEvent>
    {
        public IRequestContext Context { get; set; }

        public Message MapToMessage(MyFailingMapperEvent request, Publication publication)
        {
            throw new JsonException();
        }

        public MyFailingMapperEvent MapToRequest(Message message)
        {
            throw new JsonException();
        }
    }
}
