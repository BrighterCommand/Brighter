using System;
using Newtonsoft.Json;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles
{
    internal class FailingEventMessageMapper : IAmAMessageMapper<MyFailingMapperEvent>
    {
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
