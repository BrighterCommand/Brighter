using System;
using Greetings.Ports.Commands;
using Newtonsoft.Json.Linq;
using Paramore.Brighter;

namespace Greetings.Ports.Mappers
{
    public class GreetingReplyMessageMapper : IAmAMessageMapper<GreetingReply>
    {
        public Message MapToMessage(GreetingReply request)
        {
            var header = new MessageHeader(
                messageId: request.Id,
                topic: request.SendersAddress.Topic,
                messageType: MessageType.MT_COMMAND,
                correlationId: request.SendersAddress.CorrelationId);

            var json = new JObject(new JProperty("Id", request.Id), new JProperty("Salutation", request.Salutation));
            var body = new MessageBody(json.ToString());
            var message = new Message(header, body);
            return message;
         }

        public GreetingReply MapToRequest(Message message)
        {
            var replyAddress = new ReplyAddress(topic: message.Header.ReplyTo, correlationId: message.Header.CorrelationId);
            var reply = new GreetingReply(replyAddress);
            var body = JObject.Parse(message.Body.Value);
            reply.Id = Guid.Parse((string)body["Id"]);
            reply.Salutation = Convert.ToString(body["Salutation"]);

            return reply;
        }
    }
}
