using System.Text.Json;
using Greetings.Ports.Events;
using Paramore.Brighter;

namespace Greetings.Ports.Mappers
{
    public class GreetingEventAsyncMessageMapper : IAmAMessageMapper<GreetingAsyncEvent>
    {
        public const string Topic = "greeting.Asyncevent";

        public Message MapToMessage(GreetingAsyncEvent request)
        {
            var header = new MessageHeader(messageId: request.Id, topic: Topic, messageType: MessageType.MT_EVENT);
            var body = new MessageBody(JsonSerializer.Serialize(request, JsonSerialisationOptions.Options));
            var message = new Message(header, body);
            return message;
        }

        public GreetingAsyncEvent MapToRequest(Message message)
        {
            var greetingCommand = JsonSerializer.Deserialize<GreetingAsyncEvent>(message.Body.Value, JsonSerialisationOptions.Options);

            return greetingCommand;
        }
    }
}
