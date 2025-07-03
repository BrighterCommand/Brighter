using System.Text.Json;
using Paramore.Brighter;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.JsonConverters;

namespace ParamoreBrighter.Quartz.Tests.TestDoubles;

public class MyEventMessageMapperAsync : IAmAMessageMapperAsync<MyEvent>
{
    public IRequestContext Context { get; set; }
    public Task<Message> MapToMessageAsync(MyEvent request, Publication publication, CancellationToken cancellationToken = default)
    {
        var header = new MessageHeader(messageId: request.Id, topic: publication.Topic, messageType: request.RequestToMessageType(),
            source: publication.Source, type: publication.Type, subject: publication.Subject);
        var body = new MessageBody(JsonSerializer.Serialize(request, JsonSerialisationOptions.Options));
        var message = new Message(header, body);
        return Task.FromResult(message);
    }

    public Task<MyEvent> MapToRequestAsync(Message message, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(JsonSerializer.Deserialize<MyEvent>(message.Body.Value, JsonSerialisationOptions.Options));
    }
}
