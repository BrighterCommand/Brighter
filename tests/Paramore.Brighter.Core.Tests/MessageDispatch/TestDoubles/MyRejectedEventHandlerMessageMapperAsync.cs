using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.JsonConverters;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;

public class MyRejectedEventHandlerMessageMapperAsync : IAmAMessageMapperAsync<MyRejectedEvent>
{
    public IRequestContext? Context { get; set; }
    public Task<Message> MapToMessageAsync(MyRejectedEvent request, Publication publication, CancellationToken cancellationToken = default)
    {
        var header = new MessageHeader(messageId: request.Id, topic: publication.Topic ?? RoutingKey.Empty, source: publication.Source, 
            type: publication.Type, messageType: request.RequestToMessageType());
        var body = new MessageBody(JsonSerializer.Serialize(request, JsonSerialisationOptions.Options));
        var message = new Message(header, body);
        return Task.FromResult(message);
    }

    public Task<MyRejectedEvent> MapToRequestAsync(Message message, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(JsonSerializer.Deserialize<MyRejectedEvent>(message.Body.Value, JsonSerialisationOptions.Options)!);
    }
}
