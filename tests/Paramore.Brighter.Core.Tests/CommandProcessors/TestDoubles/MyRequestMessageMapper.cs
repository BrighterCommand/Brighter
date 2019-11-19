using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles
{
    public class MyRequestMessageMapper : IAmAMessageMapper<MyRequest>
    {
        public Message MapToMessage(MyRequest request)
        {
            var header = new MessageHeader(
                messageId: request.Id, 
                topic: "MyRequest", 
                messageType:MessageType.MT_COMMAND,
                correlationId: request.ReplyAddress.CorrelationId,
                replyTo: request.ReplyAddress.Topic);

            var json = new JObject(new JProperty("Id", request.Id), new JProperty("RequestValue", request.RequestValue));
            var body = new MessageBody(json.ToString());
            var message = new Message(header, body);
            return message;
  
        }

        public MyRequest MapToRequest(Message message)
        {
            var replyAddress = new ReplyAddress(topic: message.Header.ReplyTo, correlationId: message.Header.CorrelationId);
            var command = new MyRequest(replyAddress);
            var messageBody = JObject.Parse(message.Body.Value);
            command.Id = Guid.Parse((string)messageBody["Id"]);
            command.RequestValue = (string) messageBody["RequestValue"];
            return command;
        }
    }
}
