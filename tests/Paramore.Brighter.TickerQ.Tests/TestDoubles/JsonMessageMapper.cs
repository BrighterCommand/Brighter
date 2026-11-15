using System.Text.Json;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.JsonConverters;

namespace Paramore.Brighter.TickerQ.Tests.TestDoubles;

public class JsonMessageMapper<T> : IAmAMessageMapper<T>
    where T : class, IRequest
{
    public IRequestContext Context { get; set; }

    public Message MapToMessage(T request, Publication publication)
    {
        var header = new MessageHeader(messageId: request.Id, topic: publication.Topic, source: publication.Source,
            type: publication.Type, messageType: request.RequestToMessageType());
        var body = new MessageBody(JsonSerializer.Serialize(request, JsonSerialisationOptions.Options));
        return new Message(header, body);
    }

    public T MapToRequest(Message message)
    {
        return JsonSerializer.Deserialize<T>(message.Body.Value, JsonSerialisationOptions.Options);
    }
}

public class JsonMessageMapperAsync<T> : IAmAMessageMapperAsync<T>
    where T : class, IRequest
{
    public IRequestContext Context { get; set; }

    public Task<Message> MapToMessageAsync(T request, Publication publication, CancellationToken cancellationToken = default)
    {
        var header = new MessageHeader(messageId: request.Id, topic: publication.Topic, source: publication.Source,
            type: publication.Type, messageType: request.RequestToMessageType(), subject: publication.Subject);
        var body = new MessageBody(JsonSerializer.Serialize(request, JsonSerialisationOptions.Options));
        return Task.FromResult(new Message(header, body));
    }

    public Task<T> MapToRequestAsync(Message message, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(JsonSerializer.Deserialize<T>(message.Body.Value, JsonSerialisationOptions.Options));
    }
}
