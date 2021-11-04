using System;
using System.Text.Json;
using Greetings.Ports.Commands;
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

            var body = new MessageBody(JsonSerializer.Serialize(new GreetingsReplyBody(request.Id.ToString(), request.Salutation)));
            var message = new Message(header, body);
            return message;
         }

        public GreetingReply MapToRequest(Message message)
        {
            var replyAddress = new ReplyAddress(topic: message.Header.ReplyTo, correlationId: message.Header.CorrelationId);
            var reply = new GreetingReply(replyAddress);
            var body = JsonSerializer.Deserialize<GreetingsReplyBody>(message.Body.Value);
            reply.Id = Guid.Parse(body.Id);
            reply.Salutation = body.Salutation;

            return reply;
        }
    }
}
