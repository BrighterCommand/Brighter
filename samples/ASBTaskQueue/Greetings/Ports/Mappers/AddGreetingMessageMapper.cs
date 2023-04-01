using System.Text.Json;
using Greetings.Ports.Commands;
using Paramore.Brighter;

namespace Greetings.Ports.Mappers
{
    public class AddGreetingMessageMapper : IAmAMessageMapper<AddGreetingCommand>
    {
        public const string Topic = "greeting.addGreetingCommand";

        public Message MapToMessage(AddGreetingCommand request)
        {
            var header = new MessageHeader(messageId: request.Id, topic: Topic, messageType: MessageType.MT_COMMAND);
            var body = new MessageBody(JsonSerializer.Serialize(request, JsonSerialisationOptions.Options));
            var message = new Message(header, body);
            return message;
        }

        public AddGreetingCommand MapToRequest(Message message)
        {
            var addGreetingCommand = JsonSerializer.Deserialize<AddGreetingCommand>(message.Body.Value, JsonSerialisationOptions.Options);

            return addGreetingCommand;
        }
    }
}
