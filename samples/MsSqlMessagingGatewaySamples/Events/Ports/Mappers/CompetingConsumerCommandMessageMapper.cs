using Events.Ports.Commands;
using Newtonsoft.Json;
using Paramore.Brighter;

namespace Events.Ports.Mappers
{
    public class CompetingConsumerCommandMessageMapper : IAmAMessageMapper<CompetingConsumerCommand>
    {
        public Message MapToMessage(CompetingConsumerCommand request)
        {
            var header = new MessageHeader(messageId: request.Id, topic: "multipleconsumer.command", messageType: MessageType.MT_COMMAND);
            var body = new MessageBody(JsonConvert.SerializeObject(request));
            var message = new Message(header, body);
            return message;
        }

        public CompetingConsumerCommand MapToRequest(Message message)
        {
            var greetingCommand = JsonConvert.DeserializeObject<CompetingConsumerCommand>(message.Body.Value);
            
            return greetingCommand;
        }
    }
}
