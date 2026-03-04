using System.Text.Json;
using Paramore.Brighter;
using Paramore.Brighter.JsonConverters;

namespace Greeting.Models
{
    public class GreetingMapper : IAmAMessageMapper<GreetingEvent>
    {
        public IRequestContext? Context { get; set; }
        public GreetingMapper()
        {

        }
        public Message MapToMessage(GreetingEvent request, Publication publication)
        {
            var header = new MessageHeader(messageId: request.Id, topic: publication.Topic, messageType: MessageType.MT_EVENT);
            var body = new MessageBody(JsonSerializer.Serialize(request, JsonSerialisationOptions.Options));
            var message = new Message(header, body);
            return message;
        }

        public GreetingEvent MapToRequest(Message message)
        {
            return JsonSerializer.Deserialize<GreetingEvent>(message.Body.Bytes)!;
        }
    }
}
