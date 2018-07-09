using Newtonsoft.Json;
using Paramore.Brighter;

namespace HostedServiceTest
{
    public class GreetingEventMessageMapper : IAmAMessageMapper<GreetingEvent>
    {
        public Message MapToMessage(GreetingEvent request)
        {
            var header = new MessageHeader(messageId: request.Id, topic: "greeting.event", messageType: MessageType.MT_EVENT);
            var body = new MessageBody(JsonConvert.SerializeObject(request));
            var message = new Message(header, body);
            return message;
        }

        public GreetingEvent MapToRequest(Message message)
        {
            var greetingCommand = JsonConvert.DeserializeObject<GreetingEvent>(message.Body.Value);
            
            return greetingCommand;
        }
    }
}