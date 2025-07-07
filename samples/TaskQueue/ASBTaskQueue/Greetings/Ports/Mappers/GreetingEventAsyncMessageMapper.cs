using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Greetings.Ports.Events;
using Paramore.Brighter;
using Paramore.Brighter.JsonConverters;

namespace Greetings.Ports.Mappers
{
    public class GreetingEventAsyncMessageMapper : IAmAMessageMapperAsync<GreetingAsyncEvent>, IAmAMessageMapper<GreetingAsyncEvent>
    {
        public IRequestContext Context { get; set; }

        public Task<Message> MapToMessageAsync(GreetingAsyncEvent request, Publication publication,
            CancellationToken cancellationToken = default)
        => Task.FromResult(MapToMessage(request,publication));

        public Task<GreetingAsyncEvent> MapToRequestAsync(Message message,
            CancellationToken cancellationToken = default)
            => Task.FromResult(MapToRequest(message));

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
