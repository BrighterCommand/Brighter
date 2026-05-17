using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.JsonConverters;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles
{
    public class MyResponseMessageMapperAsync : IAmAMessageMapperAsync<MyResponse>
    {
        public IRequestContext Context { get; set; }

        public Task<Message> MapToMessageAsync(MyResponse request, Publication publication, CancellationToken cancellationToken = default)
        {
            var header = new MessageHeader(
                messageId: request.Id,
                topic: request.SendersAddress.Topic,
                messageType: request.RequestToMessageType(),
                correlationId: request.SendersAddress.CorrelationId);

            var body = new MessageBody(JsonSerializer.Serialize(new MyResponseObject(request.Id.ToString(), request.ReplyValue), JsonSerialisationOptions.Options));
            var message = new Message(header, body);
            return Task.FromResult(message);
        }

        public Task<MyResponse> MapToRequestAsync(Message message, CancellationToken cancellationToken = default)
        {
            var replyAddress = new ReplyAddress(topic: message.Header.ReplyTo, correlationId: message.Header.CorrelationId);
            var command = new MyResponse(replyAddress);
            var messageBody = JsonSerializer.Deserialize<MyResponseObject>(message.Body.Value, JsonSerialisationOptions.Options);
            command.Id = messageBody.Id;
            command.ReplyValue = messageBody.ReplyValue;
            return Task.FromResult(command);
        }
    }
}
