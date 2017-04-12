using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using Paramore.Brighter.ServiceActivator.Ports.Commands;

namespace Paramore.Brighter.ServiceActivator.Ports.Mappers
{
    public class HeartbeatReplyCommandMessageMapper : IAmAMessageMapper<HeartbeatReply>
    {
        public Message MapToMessage(HeartbeatReply request)
        {
            var header = new MessageHeader(
                messageId:request.Id,
                topic: request.SendersAddress.Topic,
                messageType: MessageType.MT_COMMAND,
                timeStamp: DateTime.UtcNow,
                correlationId: request.SendersAddress.CorrelationId
                );

            var json = new JObject(
                    new JProperty("HostName", request.HostName),
                    new JProperty("Consumers", 
                        new JArray(
                                from c in request.Consumers
                                select new JObject(
                                    new JProperty("ConnectionName", c.ConnectionName.ToString()),
                                    new JProperty("State", c.State)
                                    )
                            )
                        )
                );

            var body = new MessageBody(json.ToString());
            var message = new Message(header, body);
            return message;
        }

        public HeartbeatReply MapToRequest(Message message)
        {
            var messageBody = JObject.Parse(message.Body.Value);
            var hostName = (string) messageBody["HostName"];
            var replyAddress = new ReplyAddress(message.Header.Topic, message.Header.CorrelationId);

            var reply = new HeartbeatReply(hostName, replyAddress);
            var consumers = (JArray) messageBody["Consumers"];
            foreach (var consumer in consumers)
            {
                var connectionName = new ConnectionName((string)consumer["ConnectionName"]);
                var state = (ConsumerState)Enum.Parse(typeof (ConsumerState), (string) consumer["State"]);
                reply.Consumers.Add(new RunningConsumer(connectionName, state));
            }

            return reply;
        }
    }
}
