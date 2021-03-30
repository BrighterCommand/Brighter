using System;
using System.Text.Json;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles
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

            var body = new MessageBody(JsonSerializer.Serialize(new MyResponseObject(request.Id.ToString(), request.ReplyValue), JsonSerialisationOptions.Options));
            var message = new Message(header, body);
            return message;
        }

        public MyResponse MapToRequest(Message message)
        {
            var replyAddress = new ReplyAddress(topic: message.Header.ReplyTo, correlationId: message.Header.CorrelationId);
            var command = new MyResponse(replyAddress);
            var messageBody = JsonSerializer.Deserialize<MyResponseObject>(message.Body.Value, JsonSerialisationOptions.Options);
            command.Id = Guid.Parse(messageBody.Id);
            command.ReplyValue = messageBody.ReplyValue;
            return command;
        }
    }
}
