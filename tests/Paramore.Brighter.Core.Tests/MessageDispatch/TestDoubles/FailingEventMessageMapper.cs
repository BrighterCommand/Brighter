using System;
using Newtonsoft.Json;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles
{
    internal class FailingEventMessageMapper : IAmAMessageMapper<MyFailingMapperEvent>
    {
        public Message MapToMessage(MyFailingMapperEvent request)
        {
            throw new JsonException();
        }

        public MyFailingMapperEvent MapToRequest(Message message)
        {
            throw new JsonException();
            //return JsonConvert.DeserializeObject<MyFailingMapperEvent>(message.Body.Value);
        }
    }
}
