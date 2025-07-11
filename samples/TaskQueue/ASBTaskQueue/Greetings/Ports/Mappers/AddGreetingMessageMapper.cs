using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Greetings.Ports.Commands;
using Paramore.Brighter;
using Paramore.Brighter.JsonConverters;

namespace Greetings.Ports.Mappers
{
    public class AddGreetingMessageMapper : IAmAMessageMapper<AddGreetingCommand>, IAmAMessageMapperAsync<AddGreetingCommand>
    {
        public IRequestContext Context { get; set; }

        public Task<Message> MapToMessageAsync(AddGreetingCommand request, Publication publication,
            CancellationToken cancellationToken = default)
            => Task.FromResult(MapToMessage(request,publication));

        public Task<AddGreetingCommand> MapToRequestAsync(Message message,
            CancellationToken cancellationToken = default)
            => Task.FromResult(MapToRequest(message));

        public Message MapToMessage(AddGreetingCommand request, Publication publication)
        {
            var header = new MessageHeader(messageId: request.Id, topic: publication.Topic, messageType: MessageType.MT_COMMAND);
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
