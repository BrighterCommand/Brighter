using System;
using Newtonsoft.Json.Linq;
using Paramore.Brighter.ServiceActivator.Ports.Commands;

namespace Paramore.Brighter.ServiceActivator.Ports.Mappers
{
    public class HeartbeatRequestCommandMessageMapper : IAmAMessageMapper<HeartbeatRequest>
    {
        public Message MapToMessage(HeartbeatRequest request)
        {
            var header = new MessageHeader(
                messageId: request.Id, 
                topic: "Heartbeat", 
                messageType: MessageType.MT_COMMAND,
                correlationId: request.ReplyAddress.CorrelationId, 
                replyTo: request.ReplyAddress.Topic);

            var json = new JObject(new JProperty("Id", request.Id));
            var body = new MessageBody(json.ToString());
            var message = new Message(header, body);
            return message;

        }

        public HeartbeatRequest MapToRequest(Message message)
        {
            var replyAddress = new ReplyAddress(topic: message.Header.ReplyTo, correlationId: message.Header.CorrelationId);
            var request = new HeartbeatRequest(replyAddress);
            var messageBody = JObject.Parse(message.Body.Value);
            request.Id = Guid.Parse((string) messageBody["Id"]);
            return request;
        }
    }
}
