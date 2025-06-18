using System.Text.Json;
using Paramore.Brighter.JsonConverters;

namespace Paramore.Brighter.Transformers.JustSaying;

public class JustSayingMessageMapper<TMessage> : IAmAMessageMapper<TMessage>
    where TMessage : class, IRequest
{
    /// <inheritdoc />
    public IRequestContext? Context { get; set; }
    
    /// <inheritdoc />
    public Message MapToMessage(TMessage request, Publication publication)
    {
        if (!Guid.TryParse(request.Id, out _))
        {
            throw new InvalidOperationException("The 'Id' property need to be Guid");
        }
        
        var correlationId = GetCorrelationId();
        var tenant = GetTenant();
        var version = GetVersion();
        var messageType = request switch
        {
            Command _ => MessageType.MT_COMMAND,
            Event _ => MessageType.MT_EVENT,
            _ => MessageType.MT_DOCUMENT
        };
        
        if (request is IJustSayingRequest justSaying)
        {
            if (string.IsNullOrEmpty(justSaying.Conversation))
            {
                justSaying.Conversation = correlationId;
            }
            else
            {
                correlationId = justSaying.Conversation;
            }

            if (string.IsNullOrEmpty(justSaying.Tenant))
            {
                justSaying.Tenant = tenant;
            }
            
            if (justSaying.TimeStamp == DateTimeOffset.MinValue)
            {
                justSaying.TimeStamp = DateTimeOffset.UtcNow;
            }

            if (string.IsNullOrEmpty(justSaying.Version))
            {
                justSaying.Version = version;
            }
            
            return new Message(
                new MessageHeader(
                    correlationId: correlationId,
                    messageId: request.Id,
                    messageType: messageType,
                    subject: GetSubject(),
                    timeStamp: justSaying.TimeStamp,
                    topic: publication.Topic!),
                new MessageBody(JsonSerializer.SerializeToUtf8Bytes(request, JsonSerialisationOptions.Options)));
        }

        var timestamp = DateTimeOffset.UtcNow;

        var doc = JsonSerializer.SerializeToNode(request)!;
        doc[nameof(IJustSayingRequest.TimeStamp)] = timestamp;
        doc[nameof(IJustSayingRequest.Tenant)] = tenant;
        doc[nameof(IJustSayingRequest.Version)] = version;
        doc[nameof(IJustSayingRequest.Conversation)] = correlationId;

        return new Message(
            new MessageHeader(
                 correlationId: correlationId,
                 messageId: request.Id,
                 messageType: messageType,
                 subject: GetSubject(),
                 timeStamp: timestamp,
                 topic: publication.Topic!) ,
            new MessageBody(JsonSerializer.SerializeToUtf8Bytes(doc, JsonSerialisationOptions.Options)));
    }

    private string GetCorrelationId()
    {
        if (Context != null 
            && Context.Bag.TryGetValue(nameof(MessageHeader.CorrelationId), out var data) 
            && data is string correlationId)
        {
            return correlationId;
        }
        
        return Guid.NewGuid().ToString();
    }
    
    private string? GetTenant()
    {
        if (Context != null 
            && Context.Bag.TryGetValue(RequestContextAttributesName.Tenant, out var data) 
            && data is string tenant)
        {
            return tenant;
        }
        
        return null;
    }

    private string GetSubject()
    {
        if (Context != null 
            && Context.Bag.TryGetValue("Subject", out var data) 
            && data is string subject)
        {
            return subject;
        }

        return typeof(TMessage).Name;
    }
    
    private string GetVersion()
    {
        if (Context != null 
            && Context.Bag.TryGetValue(RequestContextAttributesName.Version, out var data) 
            && data is string version)
        {
            return version;
        }
        
        return "1.0.0";
    }

    /// <inheritdoc />
    public TMessage MapToRequest(Message message)
    {
        return JsonSerializer.Deserialize<TMessage>(message.Body.Bytes, JsonSerialisationOptions.Options)!;
    }
}
