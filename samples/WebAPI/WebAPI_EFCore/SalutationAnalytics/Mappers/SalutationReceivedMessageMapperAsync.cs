using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter;
using Paramore.Brighter.Extensions;
using SalutationApp.Requests;

namespace SalutationAnalytics.Mappers
{
    public class SalutationReceivedMessageMapperAsync : IAmAMessageMapperAsync<SalutationReceived>
    {
        public IRequestContext Context { get; set; } = null!;

        public async Task<Message> MapToMessageAsync(SalutationReceived request, Publication publication, CancellationToken cancellationToken = default)
        {
            //NOTE: We are showing an async pipeline here, but it is often overkill by comparison to using 
            //TaskCompletionSource for a Task over sync instead
            var header = new MessageHeader(messageId: request.Id, topic: publication.Topic, messageType: request.RequestToMessageType());
            using var ms = new MemoryStream();
            await JsonSerializer.SerializeAsync(ms, request, new JsonSerializerOptions(JsonSerializerDefaults.General), cancellationToken);
            var body = new MessageBody(ms.ToArray());
            return new Message(header, body);
        }

        public Task<SalutationReceived> MapToRequestAsync(Message message, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }
    }
}
