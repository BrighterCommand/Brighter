using System.Text.Json;
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

            var json = JsonSerializer.Serialize(new HeartBeatRequestBody(request.Id), JsonSerialisationOptions.Options);
            var body = new MessageBody(json);
            var message = new Message(header, body);
            return message;

        }

        public HeartbeatRequest MapToRequest(Message message)
        {
            var replyAddress = new ReplyAddress(topic: message.Header.ReplyTo, correlationId: message.Header.CorrelationId);
            var request = new HeartbeatRequest(replyAddress);
            var messageBody = JsonSerializer.Deserialize<HeartBeatRequestBody>(message.Body.Value, JsonSerialisationOptions.Options);
            request.Id = messageBody.Id;
            return request;
        }
    }
}
