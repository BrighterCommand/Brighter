using System;
using System.Linq;
using System.Text.Json;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.ServiceActivator.Ports.Commands;

namespace Paramore.Brighter.ServiceActivator.Ports.Mappers
{
    public class HeartbeatReplyCommandMessageMapper : IAmAMessageMapper<HeartbeatReply>
    {
        public IRequestContext? Context { get; set; }

        public Message MapToMessage(HeartbeatReply request, Publication publication)
        {
            var header = new MessageHeader(
                messageId: request.Id,
                topic: new RoutingKey(request.SendersAddress.Topic),
                messageType: MessageType.MT_COMMAND,
                timeStamp: DateTime.UtcNow,
                correlationId: request.SendersAddress.CorrelationId
                );

            var consumers = request.Consumers.Select(c => new HeartBeatResponseBodyConsumerObject(c.ConsumerName, c.State)).ToArray();
            var json = JsonSerializer.Serialize(new HeartBeatResponseBody(request.HostName, consumers), JsonSerialisationOptions.Options);

            var body = new MessageBody(json.ToString());
            var message = new Message(header, body);
            return message;
        }

        public HeartbeatReply MapToRequest(Message message)
        {
            var messageBody = JsonSerializer.Deserialize<HeartBeatResponseBody>(message.Body.Value, JsonSerialisationOptions.Options);

            if (messageBody is null)
                throw new ArgumentException("Request body must not be null.");
            
            if (message.Header.Topic is null || message.Header.CorrelationId is null)
                throw new ArgumentException("Reply To and Correlation Id must be set");
            
            var replyAddress = new ReplyAddress(message.Header.Topic, message.Header.CorrelationId);

            var reply = new HeartbeatReply(messageBody.HostName, replyAddress);
            foreach (var consumer in messageBody.Consumers)
            {
                var consumerName = new ConsumerName(consumer.ConsumerName);
                reply.Consumers.Add(new RunningConsumer(consumerName, consumer.State));
            }

            return reply;
        }
    }
}
