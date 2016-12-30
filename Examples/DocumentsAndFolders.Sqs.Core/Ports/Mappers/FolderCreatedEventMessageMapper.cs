using DocumentsAndFolders.Sqs.Core.Ports.Events;
using Newtonsoft.Json;
using paramore.brighter.commandprocessor;

namespace DocumentsAndFolders.Sqs.Core.Ports.Mappers
{
    public class FolderCreatedEventMessageMapper : IAmAMessageMapper<FolderCreatedEvent>
    {
        public Message MapToMessage(FolderCreatedEvent request)
        {
            var header = new MessageHeader(messageId: request.Id, topic: "FolderCreatedEvent", messageType: MessageType.MT_EVENT);
            var body = new MessageBody(JsonConvert.SerializeObject(request));
            var message = new Message(header, body);
            return message;
        }

        public FolderCreatedEvent MapToRequest(Message message)
        {
            return JsonConvert.DeserializeObject<FolderCreatedEvent>(message.Body.Value);
        }
    }
}