using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GreetingsPorts.Requests;
using Paramore.Brighter;


namespace GreetingsWeb.Mappers
{
    public class GreetingMadeMessageMapperAsync : IAmAMessageMapperAsync<GreetingMade>
    {
        public async Task<Message> MapToMessageAsync(GreetingMade request, CancellationToken cancellationToken = default)
        {
            //NOTE: We are showing an async pipeline here, but it is often overkill by comparison to using 
            //TaskCompletionSource for a Task over sync instead
            var header = new MessageHeader(messageId: request.Id, topic: "GreetingMade", messageType: MessageType.MT_EVENT);
            using var ms = new MemoryStream();
            await JsonSerializer.SerializeAsync(ms, request, new JsonSerializerOptions(JsonSerializerDefaults.General), cancellationToken);
            var body = new MessageBody(ms.ToArray());
            return new Message(header, body);
        }

        public Task<GreetingMade> MapToRequestAsync(Message message, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }
    }
}
