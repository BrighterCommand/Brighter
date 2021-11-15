﻿using System.Text.Json;
using Events.Ports.Commands;
using Paramore.Brighter;

namespace Events.Ports.Mappers
{
    public class CompetingConsumerCommandMessageMapper : IAmAMessageMapper<CompetingConsumerCommand>
    {
        public Message MapToMessage(CompetingConsumerCommand request)
        {
            var header = new MessageHeader(messageId: request.Id, topic: "multipleconsumer.command", messageType: MessageType.MT_COMMAND);
            var body = new MessageBody(JsonSerializer.Serialize(request, JsonSerialisationOptions.Options));
            var message = new Message(header, body);
            return message;
        }

        public CompetingConsumerCommand MapToRequest(Message message)
        {
            var greetingCommand = JsonSerializer.Deserialize<CompetingConsumerCommand>(message.Body.Value, JsonSerialisationOptions.Options);
            
            return greetingCommand;
        }
    }
}
