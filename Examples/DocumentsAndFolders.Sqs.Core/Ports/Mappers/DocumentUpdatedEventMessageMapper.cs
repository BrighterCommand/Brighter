using DocumentsAndFolders.Sqs.Core.Ports.Events;
using Newtonsoft.Json;
using Paramore.Brighter;

namespace DocumentsAndFolders.Sqs.Core.Ports.Mappers
{
    public class DocumentUpdatedEventMessageMapper : IAmAMessageMapper<DocumentUpdatedEvent>
    {
        public Message MapToMessage(DocumentUpdatedEvent request)
        {
            var header = new MessageHeader(messageId: request.Id, topic: "DocumentUpdatedEvent", messageType: MessageType.MT_EVENT);
            var body = new MessageBody(JsonConvert.SerializeObject(request));
            var message = new Message(header, body);
            return message;
        }

        public DocumentUpdatedEvent MapToRequest(Message message)
        {
            return JsonConvert.DeserializeObject<DocumentUpdatedEvent>(message.Body.Value);
        }
    }
}