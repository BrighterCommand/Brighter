using System.Text.Json;
using Greetings.Ports.Events;
using Paramore.Brighter;

namespace Greetings.Ports.Mappers
{
    public class GreetingEventMessageMapper : IAmAMessageMapper<GreetingEvent>
    {
        public Message MapToMessage(GreetingEvent request, Publication publication)
        {
            var header = new MessageHeader(messageId: request.Id, topic: publication.Topic, messageType: MessageType.MT_EVENT);
            var body = new MessageBody(JsonSerializer.Serialize(request, JsonSerialisationOptions.Options));
            var message = new Message(header, body);
            return message;
        }

        public GreetingEvent MapToRequest(Message message)
        {
            var greetingCommand = JsonSerializer.Deserialize<GreetingEvent>(message.Body.Value, JsonSerialisationOptions.Options);

            return greetingCommand;
        }
    }
}
