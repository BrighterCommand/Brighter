using System;
using System.Net.Mime;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.JsonConverters;

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
    
    /// <inheritdoc cref="IAmAMessageMapper{TRequest}.Context" />
    public IRequestContext? Context { get; set; }

    
    /// <inheritdoc />
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
        var justSaying = (IJustSayingRequest)request;
        justSaying.Id = GetId();
        justSaying.Conversation = GetCorrelationId(justSaying.Conversation);
        justSaying.RaisingComponent = GetRaisingComponent(justSaying.RaisingComponent);
        justSaying.Tenant = GetTenant(justSaying.Tenant);
        justSaying.Version = GetVersion(justSaying.Version);
        justSaying.TimeStamp = GetTimeStamp();
        
        return new Message(
            new MessageHeader(
                contentType: s_justSaying ,
                correlationId: justSaying.Conversation,
                messageId: justSaying.Id,
                messageType: messageType,
                subject: GetSubject(publication),
                timeStamp: justSaying.TimeStamp,
                topic: publication.Topic!),
            new MessageBody(
                JsonSerializer.SerializeToUtf8Bytes(request, JsonSerialisationOptions.Options),
                s_justSaying));

        string GetId()
        {
            if (Guid.TryParse(justSaying.Id, out _))
            {
                return Guid.NewGuid().ToString();
            }
            
            return justSaying.Id;
        }
        
        string GetCorrelationId(string? currentValue)
        {
            if (!string.IsNullOrEmpty(currentValue))
            {
                return currentValue!;
            }

            return GetFromContext(nameof(MessageHeader.CorrelationId), Guid.NewGuid().ToString())!;
        }
        
        string GetRaisingComponent(string? currentValue)
        {
            if (!string.IsNullOrEmpty(currentValue))
            {
                return currentValue!;
            }

            var context =  GetFromContext(JustSayingAttributesName.RaisingComponent);
            if (!string.IsNullOrEmpty(context))
            {
                return context!;
            }

            return publication.Source.ToString();
        }
        
        string? GetVersion(string? currentValue)
        {
            if (!string.IsNullOrEmpty(currentValue))
            {
                return currentValue!;
            }
            
            return GetFromContext(JustSayingAttributesName.Version);
        }
        
        string? GetTenant(string? currentValue)
        {
           if (!string.IsNullOrEmpty(currentValue))
           {
               return currentValue!;
           }
            
           return GetFromContext(JustSayingAttributesName.Tenant);
        }

        DateTimeOffset GetTimeStamp()
        {
            if (justSaying.TimeStamp == DateTimeOffset.MinValue)
            {
                return DateTimeOffset.UtcNow;
            }
            
            return justSaying.TimeStamp;
        }
    }

    private Message GenericToMessage(TMessage request, MessageType messageType, Publication publication)
    {
        var doc = JsonSerializer.SerializeToNode(request, JsonSerialisationOptions.Options)!;
        var correlationId = GetCorrelationId(); 
        var messageId = GetId();
        var timestamp = GetTimeStamp();
        
        doc[nameof(IJustSayingRequest.Id)] = messageId;
        doc[nameof(IJustSayingRequest.Conversation)] = correlationId;
        doc[nameof(IJustSayingRequest.RaisingComponent)] = GetRaisingComponent();
        doc[nameof(IJustSayingRequest.Tenant)] = GetTenant();
        doc[nameof(IJustSayingRequest.Version)] = GetVersion();
        doc[nameof(IJustSayingRequest.TimeStamp)] = timestamp; 
        
        return new Message(
            new MessageHeader(
                contentType: s_justSaying ,
                correlationId: correlationId,
                messageId: messageId,
                messageType: messageType,
                subject: GetSubject(publication),
                timeStamp: timestamp,
                topic: publication.Topic!),
            new MessageBody(
                doc.ToJsonString(JsonSerialisationOptions.Options),
                s_justSaying));

        string GetId()
        {
            var node = doc[nameof(IJustSayingRequest.Id)];
            if (node == null 
                || node.GetValueKind() != JsonValueKind.String 
                || !Guid.TryParse(node.GetValue<string>(), out _))
            {
                return Guid.NewGuid().ToString();
            }
            
            return node.GetValue<string>();
        }
        
        string GetCorrelationId()
        {
            var node = doc[nameof(IJustSayingRequest.Conversation)];
            if (node != null 
                && node.GetValueKind() == JsonValueKind.String
                && !string.IsNullOrEmpty(node.GetValue<string>()))
            {
                return node.GetValue<string>();
            }

            return GetFromContext(nameof(MessageHeader.CorrelationId), Guid.NewGuid().ToString())!;
        }
        
        string GetRaisingComponent()
        {
            var node = doc[nameof(IJustSayingRequest.RaisingComponent)];
            if (node != null 
                && node.GetValueKind() == JsonValueKind.String 
                && !string.IsNullOrEmpty(node.GetValue<string>()))
            {
                return node.GetValue<string>();
            }

            var context =  GetFromContext(JustSayingAttributesName.RaisingComponent);
            if (!string.IsNullOrEmpty(context))
            {
                return context!;
            }

            return publication.Source.ToString();
        }
        
        
        string? GetTenant()
        {
            var node = doc[nameof(IJustSayingRequest.Tenant)];
            if (node != null 
                && node.GetValueKind() == JsonValueKind.String 
                && !string.IsNullOrEmpty(node.GetValue<string>()))
            {
                return node.GetValue<string>();
            }
            
            return GetFromContext(JustSayingAttributesName.Tenant);
        }
        
        string? GetVersion()
        {
            var node = doc[nameof(IJustSayingRequest.Version)];
            if (node != null 
                && node.GetValueKind() == JsonValueKind.String 
                && !string.IsNullOrEmpty(node.GetValue<string>()))
            {
                return node.GetValue<string>();
            }
            
            return GetFromContext(JustSayingAttributesName.Version);
        }
        
        DateTimeOffset GetTimeStamp()
        {
            var node = doc[nameof(IJustSayingRequest.TimeStamp)];
            if (node != null
                && node.GetValueKind() == JsonValueKind.String 
                && DateTimeOffset.TryParse(node.GetValue<string>(), out var ts)
                && ts != DateTimeOffset.MinValue)
            {
                return ts;
            }
            
            return DateTimeOffset.UtcNow;
        }
    }
    
    private string GetSubject(Publication publication)
    {
        var subject = GetFromContext(JustSayingAttributesName.Subject);
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
    
    private string? GetFromContext(string headerName, string? defaultValue = null)
    {
        if (Context != null 
            && Context.Bag.TryGetValue(headerName, out var obj) 
            && obj is string data && !string.IsNullOrEmpty(data))
        {
            return data;
        }

        return defaultValue;
    }

    /// <inheritdoc />
    public TMessage MapToRequest(Message message) 
        => JsonSerializer.Deserialize<TMessage>(message.Body.Bytes, JsonSerialisationOptions.Options)!;
}

