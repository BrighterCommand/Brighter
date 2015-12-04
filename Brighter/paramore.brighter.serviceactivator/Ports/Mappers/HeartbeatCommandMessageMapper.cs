using System.Reflection;
using Newtonsoft.Json;
using paramore.brighter.commandprocessor;
using paramore.brighter.serviceactivator.Ports.Commands;

namespace paramore.brighter.serviceactivator.Ports.Mappers
{
    public class HeartbeatCommandMessageMapper : IAmAMessageMapper<HeartbeatCommand>
    {
        public Message MapToMessage(HeartbeatCommand request)
        {
            var header = new MessageHeader(messageId: request.Id, topic: "Heartbeat", messageType: MessageType.MT_COMMAND);
            var body = new MessageBody(JsonConvert.SerializeObject(request));
            var message = new Message(header, body);
            return message;

        }

        public HeartbeatCommand MapToRequest(Message message)
        {
            return JsonConvert.DeserializeObject<HeartbeatCommand>(message.Body.Value);
        }
    }
}
