using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

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
            
            return new Message(
                new MessageHeader(
                    messageId: request.Id,
                    topic: publication.Topic!,
                    messageType: MessageType.MT_EVENT,
                    correlationId: correlationId)
                {
                    TimeStamp = justSaying.TimeStamp,
                    Bag = { ["Subject"] = typeof(TMessage).Name }
                },
                new MessageBody(JsonSerializer.SerializeToUtf8Bytes(request, JsonSerialisationOptions.Options)));
        }

        var timestamp = DateTimeOffset.UtcNow;

        var doc = JsonSerializer.SerializeToNode(request)!;
        doc[nameof(IJustSayingRequest.TimeStamp)] = timestamp;
        doc[nameof(IJustSayingRequest.Tenant)] = tenant;
        doc[nameof(IJustSayingRequest.Conversation)] = correlationId;

        return new Message(
            new MessageHeader(
                messageId: request.Id,
                topic: publication.Topic!,
                messageType: MessageType.MT_EVENT,
                correlationId: correlationId)
            {
                TimeStamp = timestamp,
                Bag = { ["Subject"] = typeof(TMessage).Name }
            },
            new MessageBody(JsonSerializer.SerializeToUtf8Bytes(doc, JsonSerialisationOptions.Options)));
    }

    private string GetCorrelationId()
    {
        if (Context != null && Context.Bag.TryGetValue(nameof(MessageHeader.CorrelationId), out var data) && data is string correlationId)
        {
            return correlationId;
        }
        
        return Guid.NewGuid().ToString();
    }
    
    private string GetTenant()
    {
        if (Context != null && Context.Bag.TryGetValue(nameof(IJustSayingRequest.Tenant), out var data) && data is string tenant)
        {
            return tenant;
        }
        
        return "all";
    }

    /// <inheritdoc />
    public TMessage MapToRequest(Message message)
    {
        return JsonSerializer.Deserialize<TMessage>(message.Body.Bytes, JsonSerialisationOptions.Options)!;
    }


    public class JustSayingMessaging
    {
        [JsonPropertyName("TimeStamp")]
        public DateTime TimeStamp { get; set; }

        [JsonPropertyName("RaisingComponent")]
        public string? RaisingComponent { get; set; }

        [JsonPropertyName("Version")]
        public string? Version { get; set; }

        [JsonPropertyName("SourceIp")]
        public string? SourceIp { get; set; }

        [JsonPropertyName("Tenant")]
        public string Tenant { get; set; } = "all";

        [JsonPropertyName("Conversation")]
        public string? Conversation { get; set; }
        
        [JsonExtensionData]
        public Dictionary<string, JsonElement?>? Data { get; set; }
    }
}
