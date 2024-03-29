﻿using System.Text.Json;

namespace Paramore.Brighter.AWS.Tests.TestDoubles
{
    internal class MyDeferredCommandMessageMapper : IAmAMessageMapper<MyDeferredCommand>
    {
        public Message MapToMessage(MyDeferredCommand request, Publication publication)
        {
            var header = new MessageHeader(messageId: request.Id, topic: publication.Topic, messageType: MessageType.MT_COMMAND);
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
