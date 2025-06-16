using System.Text.Json;
using Greetings.Ports.Events;
using Paramore.Brighter;
using Paramore.Brighter.JsonConverters;

namespace Greetings.Ports.Mappers
{
    public class GreetingEventAsyncMessageMapper : IAmAMessageMapper<GreetingAsyncEvent>
    {
        public IRequestContext Context { get; set; }

        public Message MapToMessage(GreetingAsyncEvent request, Publication publication)
        {
            var header = new MessageHeader(messageId: request.Id, topic: publication.Topic, messageType: MessageType.MT_EVENT);
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
