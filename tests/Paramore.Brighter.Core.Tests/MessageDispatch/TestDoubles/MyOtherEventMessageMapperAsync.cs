using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.JsonConverters;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;

internal sealed class MyOtherEventMessageMapperAsync : IAmAMessageMapperAsync<MyOtherEvent>
{
    public IRequestContext Context { get; set; }

    public Task<Message> MapToMessageAsync(MyOtherEvent request, Publication publication, CancellationToken ct = default)
    {
        var header = new MessageHeader(messageId: request.Id, topic: publication.Topic ?? RoutingKey.Empty, messageType: request.RequestToMessageType(),
            source: publication.Source, type: publication.Type, subject: publication.Subject);
        var body = new MessageBody(JsonSerializer.Serialize(request, JsonSerialisationOptions.Options));
        var message = new Message(header, body);
        return Task.FromResult(message);
    }

    public Task<MyOtherEvent> MapToRequestAsync(Message message, CancellationToken ct = default)
    {
        return Task.FromResult(JsonSerializer.Deserialize<MyOtherEvent>(message.Body.Value, JsonSerialisationOptions.Options))!;
    }
}
