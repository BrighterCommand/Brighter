using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Transforms.Attributes;

namespace Paramore.Brighter.MessageMappers;

/// <summary>
/// A generic message mapper that serializes and deserializes request objects of type <typeparamref name="TRequest"/>
/// to and from JSON format for use with Brighter's messaging infrastructure. This mapper implements both synchronous
/// and asynchronous interfaces for mapping messages. It supports mapping both <see cref="ICommand"/> and <see cref="IEvent"/> types. 
/// </summary>
/// <typeparam name="TRequest">The message type.</typeparam>
public class JsonMessageMapper<TRequest> : IAmAMessageMapper<TRequest>, IAmAMessageMapperAsync<TRequest> where TRequest : class, IRequest
{
    /// <inheritdoc cref="IAmAMessageMapper{TRequest}.Context"/>
    public IRequestContext? Context { get; set; }

    /// <inheritdoc />
    [CloudEvents(0)]
    public Task<Message> MapToMessageAsync(TRequest request, Publication publication,
        CancellationToken cancellationToken = default)
        => Task.FromResult(MapToMessage(request, publication));

    /// <inheritdoc />
    public Task<TRequest> MapToRequestAsync(Message message, CancellationToken cancellationToken = default)
        => Task.FromResult(MapToRequest(message));

    /// <inheritdoc />
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

        var header = new MessageHeader(messageId: request.Id, topic: publication.Topic, messageType: messageType, contentType: MessageBody.APPLICATION_JSON);

        var body = new MessageBody(JsonSerializer.Serialize(request, JsonSerialisationOptions.Options));
        var message = new Message(header, body);
        return message;
    }

    /// <inheritdoc />
    public TRequest MapToRequest(Message message)
    {
        var request = JsonSerializer.Deserialize<TRequest>(message.Body.Value, JsonSerialisationOptions.Options);

        if (request is null)
            throw new ArgumentException($"Unable to deseralise message body for {message.Header.Topic}");

        return request;
    }
}
