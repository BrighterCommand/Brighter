using System.Text.Json;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.JsonConverters;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles
{
    public class MyRequestMessageMapper : IAmAMessageMapper<MyRequest>
    {
        public IRequestContext Context { get; set; }

        public Message MapToMessage(MyRequest request, Publication publication)
        {
            var header = new MessageHeader(
                messageId: request.Id, 
                topic: publication.Topic, 
                messageType:request.RequestToMessageType(),
                correlationId: request.ReplyAddress.CorrelationId,
                replyTo: request.ReplyAddress.Topic);

            var body = new MessageBody(JsonSerializer.Serialize(request.RequestValue, JsonSerialisationOptions.Options));
            var message = new Message(header, body);
            return message;
  
        }

        public MyRequest MapToRequest(Message message)
        {
            var replyAddress = new ReplyAddress(topic: message.Header.ReplyTo, correlationId: message.Header.CorrelationId);
            var command = new MyRequest(replyAddress);
            var myRequest = JsonSerializer.Deserialize<MyRequest>(message.Body.Value, JsonSerialisationOptions.Options);
            command.Id = myRequest.Id;
            command.RequestValue = myRequest.RequestValue;
            return command;
        }
    }
}
