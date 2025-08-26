using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter;
using Paramore.Brighter.JsonConverters;

namespace GreetingsApp.Events.Mappers
{
    public class GreetingEventMessageMapper : IAmAMessageMapper<GreetingEvent>, IAmAMessageMapperAsync<GreetingEvent>
    {
        public IRequestContext Context { get; set; }
        
        public Task<Message> MapToMessageAsync(GreetingEvent request, Publication publication,
            CancellationToken cancellationToken = default)
            => Task.FromResult(MapToMessage(request,publication));

        public Task<GreetingEvent> MapToRequestAsync(Message message,
            CancellationToken cancellationToken = default)
            => Task.FromResult(MapToRequest(message));

        public Message MapToMessage(GreetingEvent request, Publication publication)
        {
            var header = new MessageHeader(messageId: request.Id, topic: publication.Topic, messageType: MessageType.MT_EVENT);
            var body = new MessageBody(JsonSerializer.Serialize(request, JsonSerialisationOptions.Options));
            var message = new Message(header, body);
            return message;
        }

        public GreetingEvent MapToRequest(Message message)
        {
            var greetingCommand = JsonSerializer.Deserialize<GreetingEvent>(message.Body.Value, JsonSerialisationOptions.Options);

            return greetingCommand;
        }
    }
}
