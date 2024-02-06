using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Paramore.Brighter;
using SalutationPorts.Requests;

namespace SalutationAnalytics.Mappers
{
    public class SalutationReceivedMessageMapperAsync : IAmAMessageMapperAsync<SalutationReceived>
    {
        public async Task<Message> MapToMessage(SalutationReceived request)
        {
            //NOTE: We are showing an async pipeline here, but it is often overkill by comparison to using 
            //TaskCompletionSource for a Task over sync instead
            var header = new MessageHeader(messageId: request.Id, topic: "SalutationReceived", messageType: MessageType.MT_EVENT);
            using var ms = new MemoryStream();
            await JsonSerializer.SerializeAsync(ms, request, new JsonSerializerOptions(JsonSerializerDefaults.General));
            var body = new MessageBody(ms.ToArray());
            return new Message(header, body);
        }

        public Task<SalutationReceived> MapToRequest(Message message)
        {
            throw new System.NotImplementedException();
        }
    }
}
