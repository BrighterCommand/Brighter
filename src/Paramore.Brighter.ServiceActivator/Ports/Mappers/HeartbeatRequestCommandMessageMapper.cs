using System;
using System.Text.Json;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.ServiceActivator.Ports.Commands;

namespace Paramore.Brighter.ServiceActivator.Ports.Mappers
{
    public class HeartbeatRequestCommandMessageMapper : IAmAMessageMapper<HeartbeatRequest>
    {
        public IRequestContext? Context { get; set; }

        public Message MapToMessage(HeartbeatRequest request, Publication publication)
        {
            var header = new MessageHeader(
                messageId: request.Id, 
                topic: new RoutingKey("Heartbeat"), 
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
            if (message.Header.ReplyTo is null || message.Header.CorrelationId is null)
                throw new ArgumentException("Reply To and Correlation Id must be set");
            
            var replyAddress = new ReplyAddress(topic: message.Header.ReplyTo, correlationId: message.Header.CorrelationId);
            var request = new HeartbeatRequest(replyAddress);
            var messageBody = JsonSerializer.Deserialize<HeartBeatRequestBody>(message.Body.Value, JsonSerialisationOptions.Options);

            if (messageBody is null)
                throw new ArgumentException("Message Body must not be null");
            
            request.Id = messageBody.Id;
            return request;
        }
    }
}
