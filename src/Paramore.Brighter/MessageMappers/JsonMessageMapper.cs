using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.JsonConverters;
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
    [CloudEvents(0)]
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

 #if NETSTANDARD2_0
        var header = new MessageHeader(messageId: request.Id, topic: publication.Topic, messageType: messageType, contentType: new ContentType("application/json"),
            source: publication.Source, type: publication.Type, correlationId: request.CorrelationId, replyTo: publication.ReplyTo ?? RoutingKey.Empty, dataSchema: publication.DataSchema, subject: publication.Subject,  partitionKey: Context.GetPartitionKey());
 #else       
        var header = new MessageHeader(messageId: request.Id, topic: publication.Topic, messageType: messageType, contentType: new ContentType(MediaTypeNames.Application.Json),
            source: publication.Source, type: publication.Type, correlationId: request.CorrelationId, replyTo: publication.ReplyTo ?? RoutingKey.Empty, dataSchema: publication.DataSchema, subject: publication.Subject,  partitionKey: Context.GetPartitionKey());
#endif
        var defaultHeaders = publication.DefaultHeaders ?? new Dictionary<string, object>();
        header.Bag = defaultHeaders.Merge(Context.GetHeaders());

        var body = new MessageBody(JsonSerializer.Serialize(request, JsonSerialisationOptions.Options));
        var message = new Message(header, body);
        return message;
    }

    /// <inheritdoc />
    public TRequest MapToRequest(Message message)
    {
        var request = JsonSerializer.Deserialize<TRequest>(message.Body.Value, JsonSerialisationOptions.Options);

        if (request is null)
        {
            throw new ArgumentException($"Unable to deseralise message body for {message.Header.Topic}");
        }

        if (Context != null)
        {
            Context.Bag[RequestContextBagNames.Headers] = message.Header.Bag;
            Context.Bag[RequestContextBagNames.TimeStamp] = message.Header.TimeStamp;
            Context.Bag[RequestContextBagNames.Source] = message.Header.Source;
            Context.Bag[RequestContextBagNames.Type] = message.Header.Type;
            
            if (!PartitionKey.IsNullOrEmpty(message.Header.PartitionKey))
            {
                Context.Bag[RequestContextBagNames.PartitionKey] = message.Header.PartitionKey;
            }

            if (!Id.IsNullOrEmpty(message.Header.JobId))
            {
                Context.Bag[RequestContextBagNames.JobId] = message.Header.JobId;
            }
            
            if (!Id.IsNullOrEmpty(message.Header.WorkflowId))
            {
                Context.Bag[RequestContextBagNames.WorkflowId] = message.Header.WorkflowId;
            }
            
            if (!Id.IsNullOrEmpty(message.Header.CorrelationId))
            {
                Context.Bag[RequestContextBagNames.CorrelationId] = message.Header.CorrelationId;
            }
            
            if (!string.IsNullOrEmpty(message.Header.Subject))
            {
                Context.Bag[RequestContextBagNames.Subject] = message.Header.Subject!;
            }
            
            if (!RoutingKey.IsNullOrEmpty(message.Header.ReplyTo))
            {
                Context.Bag[RequestContextBagNames.ReplyTo] = message.Header.ReplyTo;
            }
        }

        return request;
    }
}
