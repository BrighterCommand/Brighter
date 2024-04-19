﻿using System.Text.Json;
using Paramore.Brighter.Extensions;

namespace Paramore.Brighter.RMQ.Tests.TestDoubles
{
    internal class MyDeferredCommandMessageMapper : IAmAMessageMapper<MyDeferredCommand>
    {
        public Message MapToMessage(MyDeferredCommand request, Publication publication)
        {
            var header = new MessageHeader(messageId: request.Id, topic: publication.Topic, messageType: request.RequestToMessageType());
            var body = new MessageBody(System.Text.Json.JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.General)));
            var message = new Message(header, body);
            return message;
        }

        public MyDeferredCommand MapToRequest(Message message)
        {
            return JsonSerializer.Deserialize<MyDeferredCommand>(message.Body.Value, JsonSerialisationOptions.Options);
        }
    }
}
