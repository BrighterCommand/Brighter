using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Transformers.JustSaying.Extensions;
using Paramore.Brighter.Transformers.JustSaying.JsonConverters;
using Paramore.Brighter.Transforms.Attributes;

namespace Paramore.Brighter.Transformers.JustSaying;

/// <summary>
/// Provides interoperability between Brighter and JustSaying libraries by mapping messages.
/// </summary>
/// <remarks>
/// For optimal performance, ensure the message type implements <see cref="IJustSayingRequest"/> 
/// or inherits from <see cref="JustSayingCommand"/>/<see cref="JustSayingEvent"/>.
/// This allows the mapper to efficiently map headers and body without relying on JSON node manipulation.
/// Messages not implementing these interfaces will be processed using a generic JSON approach,
/// which may have higher overhead due to dynamic JSON structure manipulation.
/// </remarks>
/// <typeparam name="TMessage">The request type, must be a class implementing IRequest.</typeparam>
public class JustSayingMessageMapper<TMessage> : IAmAMessageMapper<TMessage>, IAmAMessageMapperAsync<TMessage>
    where TMessage : class, IRequest
{
    // ReSharper disable once StaticMemberInGenericType
    private static readonly ContentType s_justSaying = new("application/json");

    /// <summary>
    /// Initialize <see cref="JustSayingMessageMapper{TMessage}"/>
    /// </summary>
    public JustSayingMessageMapper()
    {
        RegisterConverters.Register();
    }
    
    /// <inheritdoc cref="IAmAMessageMapper{TRequest}.Context" />
    public IRequestContext? Context { get; set; }

    /// <inheritdoc />
    [CloudEvents(0)]
    public Task<Message> MapToMessageAsync(TMessage request, Publication publication, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(MapToMessage(request, publication));
    }

    /// <inheritdoc />
    public Task<TMessage> MapToRequestAsync(Message message, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(MapToRequest(message));
    }

    /// <inheritdoc />
    [CloudEvents(0)]
    public Message MapToMessage(TMessage request, Publication publication)
    {
        var messageType = request switch
        {
            Command _ => MessageType.MT_COMMAND,
            Event _ => MessageType.MT_EVENT,
            _ => MessageType.MT_DOCUMENT
        };
        
        return request is IJustSayingRequest ? JustSayingToMessage(request, messageType, publication) : GenericToMessage(request, messageType, publication);
    }

    private Message JustSayingToMessage(TMessage request, MessageType messageType, Publication publication)
    {
        var defaultHeaders = publication.DefaultHeaders ?? new Dictionary<string, object>();
        var justSaying = (IJustSayingRequest)request;
        justSaying.Id = GetId(justSaying.Id);
        justSaying.Conversation = GetCorrelationId(justSaying.Conversation);
        justSaying.RaisingComponent = GetRaisingComponent(publication, justSaying.RaisingComponent);
        justSaying.Tenant = GetTenant(justSaying.Tenant);
        justSaying.Version = GetVersion(justSaying.Version);
        justSaying.TimeStamp = GetTimeStamp(justSaying.TimeStamp);
        
        return new Message(
            new MessageHeader(
                contentType: s_justSaying ,
                correlationId: justSaying.Conversation,
                messageId: justSaying.Id,
                messageType: messageType,
                subject: GetSubject(publication),
                timeStamp: justSaying.TimeStamp,
                topic: publication.Topic!,
                partitionKey: Context.GetPartitionKey())
            {
                Bag = defaultHeaders.Merge(Context.GetHeaders())
            },
            new MessageBody(
                JsonSerializer.SerializeToUtf8Bytes(request, JsonSerialisationOptions.Options),
                s_justSaying));
    }

    private Message GenericToMessage(TMessage request, MessageType messageType, Publication publication)
    {
        var defaultHeaders = publication.DefaultHeaders ?? new Dictionary<string, object>();
        var doc = JsonSerializer.SerializeToNode(request, JsonSerialisationOptions.Options)!;
        var messageId = GetId(doc.GetId(nameof(IJustSayingRequest.Id)));
        var correlationId = GetCorrelationId(doc.GetId(nameof(IJustSayingRequest.Conversation))); 
        var timestamp = GetTimeStamp(doc.GetDateTimeOffset(nameof(IJustSayingRequest.TimeStamp)));
        
        doc[nameof(IJustSayingRequest.Id)] = messageId.Value;
        doc[nameof(IJustSayingRequest.Conversation)] = correlationId.Value;
        doc[nameof(IJustSayingRequest.RaisingComponent)] = GetRaisingComponent(publication, doc.GetString(nameof(IJustSayingRequest.RaisingComponent)));
        doc[nameof(IJustSayingRequest.Tenant)] = GetTenant(doc.GetString(nameof(IJustSayingRequest.Tenant), string.Empty)!)?.Value;
        doc[nameof(IJustSayingRequest.Version)] = GetVersion(doc.GetString(nameof(IJustSayingRequest.Version)));
        doc[nameof(IJustSayingRequest.TimeStamp)] = timestamp; 
        
        return new Message(
            new MessageHeader(
                contentType: s_justSaying ,
                correlationId: correlationId,
                messageId: messageId,
                messageType: messageType,
                subject: GetSubject(publication),
                timeStamp: timestamp,
                topic: publication.Topic!,
                partitionKey: Context.GetPartitionKey())
            {
                Bag = defaultHeaders.Merge(Context.GetHeaders())
            },
            new MessageBody(
                doc.ToJsonString(JsonSerialisationOptions.Options),
                s_justSaying));
    }
    
    private string GetSubject(Publication publication)
    {
        var subject = Context.GetFromBag<string>(JustSayingAttributesName.Subject);
        if (!string.IsNullOrEmpty(subject))
        {
            return subject!;
        }

        if (!string.IsNullOrEmpty(publication.Subject))
        {
            return publication.Subject!;
        }

        if (publication.RequestType != null)
        {
            return publication.RequestType.Name;
        }

        return typeof(TMessage).Name;
    }
    
    private string GetRaisingComponent(Publication publication, string? raisingComponent)
    {
        if (!string.IsNullOrEmpty(raisingComponent))
        {
            return raisingComponent!;
        }

        raisingComponent = Context.GetFromBag<string>(JustSayingAttributesName.RaisingComponent);
        if (!string.IsNullOrEmpty(raisingComponent))
        {
            return raisingComponent!;
        }

        return publication.Source.ToString();
    }
    
    private Id GetId(Id? id)
    {
        return Guid.TryParse(id?.Value, out _) ? id! : Id.Random();
    }
    
    private Id GetCorrelationId(Id? currentValue)
    {
        if (!Id.IsNullOrEmpty(currentValue))
        {
            return currentValue;
        }

        return Context.GetIdFromBag(JustSayingAttributesName.Conversation, Id.Random())!;
    }
    
    private Tenant? GetTenant(Tenant? currentValue)
    {
        if (!Tenant.IsNullOrEmpty(currentValue))
        {
            return currentValue;
        }
            
        var val = Context.GetFromBag(JustSayingAttributesName.Tenant);
        return val switch
        {
            string valString when !string.IsNullOrEmpty(valString) => new Tenant(valString),
            Tenant tenant when Tenant.IsNullOrEmpty(tenant) => tenant,
            _ => (Tenant?)null
        };
    }
    
    private string? GetVersion(string? currentValue)
    {
        if (!string.IsNullOrEmpty(currentValue))
        {
            return currentValue!;
        }
            
        return Context.GetFromBag<string>(JustSayingAttributesName.Version);
    }
    
    private static DateTimeOffset GetTimeStamp(DateTimeOffset currentValue)
    {
        if (currentValue == DateTimeOffset.MinValue)
        {
            return DateTimeOffset.UtcNow;
        }
            
        return currentValue;
    }

    /// <inheritdoc />
    public TMessage MapToRequest(Message message) 
        => JsonSerializer.Deserialize<TMessage>(message.Body.Bytes, JsonSerialisationOptions.Options)!;
}

