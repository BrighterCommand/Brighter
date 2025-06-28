using System.Text.Json;
using Events.Ports.Commands;
using Paramore.Brighter;
using Paramore.Brighter.JsonConverters;

namespace Events.Ports.Mappers
{
    public class CompetingConsumerCommandMessageMapper : IAmAMessageMapper<CompetingConsumerCommand>
    {
        public IRequestContext Context { get; set; }

        public Message MapToMessage(CompetingConsumerCommand request, Publication publication)
        {
            var header = new MessageHeader(messageId: request.Id, topic: new RoutingKey("multipleconsumer.command"), messageType: MessageType.MT_COMMAND);
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
