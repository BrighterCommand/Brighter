using System;
using Newtonsoft.Json.Linq;

namespace Paramore.Brighter.Tests.CommandProcessors.TestDoubles
{
    public class MyResponseMessageMapper : IAmAMessageMapper<MyResponse>
    {
        public Message MapToMessage(MyResponse request)
        {
            var header = new MessageHeader(
                messageId: request.Id,
                topic: request.SendersAddress.Topic,
                messageType: MessageType.MT_COMMAND,
                correlationId: request.SendersAddress.CorrelationId);

            var json = new JObject(new JProperty("Id", request.Id), new JProperty("ReplyValue", request.ReplyValue));
            var body = new MessageBody(json.ToString());
            var message = new Message(header, body);
            return message;
        }

        public MyResponse MapToRequest(Message message)
        {
            var replyAddress = new ReplyAddress(topic: message.Header.ReplyTo, correlationId: message.Header.CorrelationId);
            var command = new MyResponse(replyAddress);
            var messageBody = JObject.Parse(message.Body.Value);
            command.Id = Guid.Parse((string)messageBody["Id"]);
            command.ReplyValue = (string) messageBody["ReplyValue"];
            return command;
        }
    }
}
