using System.Text.Json;
using GreetingsPorts.Requests;
using Paramore.Brighter;


namespace Greetingsweb.Mappers
{
    public class GreetingMadeMessageMapper : IAmAMessageMapper<GreetingMade>
    {
        public Message MapToMessage(GreetingMade request)
        {
            var header = new MessageHeader(messageId: request.Id, topic: "GreetingMade", messageType: MessageType.MT_EVENT);
            var body = new MessageBody(System.Text.Json.JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.General)));
            var message = new Message(header, body);
            return message;
        }

        public GreetingMade MapToRequest(Message message)
        {
            throw new System.NotImplementedException();
        }
    }
}
