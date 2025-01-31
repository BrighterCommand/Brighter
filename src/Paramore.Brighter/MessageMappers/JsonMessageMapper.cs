using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.MessageMappers;

public class JsonMessageMapper<TRequest> : IAmAMessageMapper<TRequest>, IAmAMessageMapperAsync<TRequest> where TRequest : class, IRequest
{
    public IRequestContext? Context { get; set; }

    public Task<Message> MapToMessageAsync(TRequest request, Publication publication,
        CancellationToken cancellationToken = default)
        => Task.FromResult(MapToMessage(request, publication));

    public Task<TRequest> MapToRequestAsync(Message message, CancellationToken cancellationToken = default)
        => Task.FromResult(MapToRequest(message));

    public Message MapToMessage(TRequest request, Publication publication)
    {
        MessageType messageType = request switch
        {
            ICommand => MessageType.MT_COMMAND,
            IEvent => MessageType.MT_EVENT,
            _ => throw new ArgumentException(@"This message mapper can only map Commands and Events", nameof(request))
        };

        if(publication.Topic is null)
            throw new ArgumentException($"No Topic Defined for {publication}");

        var header = new MessageHeader(messageId: request.Id, topic: publication.Topic, messageType: messageType);

        var body = new MessageBody(JsonSerializer.Serialize(request, JsonSerialisationOptions.Options));
        var message = new Message(header, body);
        return message;
    }

    public TRequest MapToRequest(Message message)
    {
        var request = JsonSerializer.Deserialize<TRequest>(message.Body.Value, JsonSerialisationOptions.Options);

        if (request is null)
            throw new ArgumentException($"Unable to deseralise message body for {message.Header.Topic}");

        return request;
    }
}
