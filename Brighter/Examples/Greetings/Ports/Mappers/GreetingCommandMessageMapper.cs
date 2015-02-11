using Greetings.Ports.Commands;
using Newtonsoft.Json;
using paramore.brighter.commandprocessor;

namespace Greetings.Ports.Mappers
{
    internal class GreetingCommandMessageMapper : IAmAMessageMapper<GreetingCommand>
    {

        public Message MapToMessage(GreetingCommand request)
        {
            var header = new MessageHeader(messageId: request.Id, topic: "greeting.command", messageType: MessageType.MT_COMMAND);
            var body = new MessageBody(JsonConvert.SerializeObject(request));
            var message = new Message(header, body);
            return message;
        }

        public GreetingCommand MapToRequest(Message message)
        {
            return JsonConvert.DeserializeObject<GreetingCommand>(message.Body.Value);
        }
    }
}
