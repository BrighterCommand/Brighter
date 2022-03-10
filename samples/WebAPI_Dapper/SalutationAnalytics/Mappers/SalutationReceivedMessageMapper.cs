using System.Text.Json;
using Paramore.Brighter;
using SalutationPorts.Requests;

namespace SalutationAnalytics.Mappers
{
    public class SalutationReceivedMessageMapper : IAmAMessageMapper<SalutationReceived>
    {
        public Message MapToMessage(SalutationReceived request)
        {
            var header = new MessageHeader(messageId: request.Id, topic: "SalutationReceived", messageType: MessageType.MT_EVENT);
            var body = new MessageBody(System.Text.Json.JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.General)));
            var message = new Message(header, body);
            return message;
         }

        public SalutationReceived MapToRequest(Message message)
        {
            throw new System.NotImplementedException();
        }
    }
}
