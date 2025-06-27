using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.JsonConverters;

namespace Paramore.Brighter.Transformers.MassTransit;

/// <summary>
/// Maps messages between Brighter and MassTransit using MassTransit's default envelope format.
/// </summary>
/// <remarks>
/// <para>
/// This mapper integrates Brighter with MassTransit by wrapping requests in a MassTransit-compatible 
/// envelope structure. It ensures messages adhere to MassTransit's expected payload format when 
/// using default configurations, including required metadata like CorrelationId, HostInfo, and 
/// routing addresses.
/// </para>
/// <para>
/// <strong>Key Features:</strong>
/// - Uses <see cref="MassTransitMessageEnvelop{TMessage}"/> to match MassTransit's default payload structure
/// - Sets standard MassTransit headers (e.g., DestinationAddress, SourceAddress) from context or defaults
/// - Serializes messages with <c>application/vnd.masstransit+json</c> content type [[7]]
/// - Supports both synchronous and asynchronous mapping operations
/// </para>
/// <para>
/// <strong>Usage Notes:</strong>
/// - Designed for MassTransit's default configuration; custom MassTransit setups may require adjustments
/// - Ensure <see cref="MassTransitMessageEnvelop{TMessage}"/> matches MassTransit's runtime expectations
/// - Leverages <see cref="IRequestContext"/> for header values with fallback to publication metadata
/// </para>
/// </remarks>
public class MassTransitMessageMapper<TMessage> : IAmAMessageMapper<TMessage>, IAmAMessageMapperAsync<TMessage>
    where TMessage : class, IRequest 
{
    private static readonly HostInfo?  s_hostInfo = HostInfo.Create();
    
    /// <summary>
    /// The Masstransit content-type.
    /// </summary>
    public static ContentType MassTransitContentType { get; }= new("application/vnd.masstransit+json");
    
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
    public virtual Message MapToMessage(TMessage request, Publication publication)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var bag = new Dictionary<string, object?>();
        if (Context != null && Context.Bag.Count == 0)
        {
            foreach (var pair in Context.Bag)
            {
                if (!pair.Key.StartsWith(MassTransitHeaderNames.HeaderPrefix))
                {
                    bag[pair.Key] = pair.Value;
                }
            }
        }
        
        var envelop = new MassTransitMessageEnvelop<TMessage>
        {
            ConversationId = GetConversationId(),
            CorrelationId = GetCorrelationId(),
            DestinationAddress = GetDestinationAddress(), 
            ExpirationTime = GetExpirationTime(), 
            FaultAddress = GetFaultAddress(), 
            Headers = null,
            Host = s_hostInfo,
            InitiatorId = GetInitiatorId(), 
            Message = request,
            MessageId = request.Id,
            MessageType = GetMessageType(),
            RequestId = GetRequestId(),
            ResponseAddress = GetResponseAddress(publication),
            SourceAddress = GetSourceAddress(publication),
            SentTime = timestamp.DateTime 
        };

        return new Message(
            new MessageHeader(
                contentType: MassTransitContentType,
                correlationId: envelop.CorrelationId,
                messageId: envelop.MessageId,
                messageType: request switch
                {
                    Command _ => MessageType.MT_COMMAND,
                    Event _ => MessageType.MT_EVENT,
                    _ => MessageType.MT_DOCUMENT
                },
                timeStamp: timestamp,
                topic: publication.Topic!),
            new MessageBody(JsonSerializer.SerializeToUtf8Bytes(envelop, JsonSerialisationOptions.Options), MassTransitContentType)
        );
    }
    
    private string GetCorrelationId()
    {
        return GetFromContext(nameof(MessageHeader.CorrelationId), Guid.NewGuid().ToString())!;
    }
    
    private string? GetConversationId() => GetFromContext(MassTransitHeaderNames.ConversationId);
    
    private string? GetDestinationAddress() => GetFromContext(MassTransitHeaderNames.DestinationAddress);

    private DateTime? GetExpirationTime()
    {
        if (Context != null && Context.Bag.TryGetValue(MassTransitHeaderNames.ExpirationTime, out var val))
        {
            if (val is DateTimeOffset offset)
            {
                return offset.DateTime;
            }

            if (val is DateTime dateTime)
            {
                return dateTime;
            }
        }
        
        return null;
    }
    
    private string? GetFaultAddress() => GetFromContext(MassTransitHeaderNames.FaultAddress);
    
    private string? GetInitiatorId() => GetFromContext(MassTransitHeaderNames.InitiatorId);
    
    private string? GetRequestId() => GetFromContext(MassTransitHeaderNames.RequestId);
    
    private string? GetResponseAddress(Publication publication)
    {
        var response =  GetFromContext(MassTransitHeaderNames.ResponseAddress);
        if (!string.IsNullOrEmpty(response))
        {
            return response;
        }

        return null;
    }
    
    private string? GetSourceAddress(Publication publication)
    {
        var source = GetFromContext(MassTransitHeaderNames.SourceAddress);
        if (!string.IsNullOrEmpty(source))
        {
            return source;
        }
        
        return publication.Source.ToString();
    }
    
    private string[]? GetMessageType()
    {
        if (Context != null && Context.Bag.TryGetValue(MassTransitHeaderNames.MessageType, out var obj))
        {
            if (obj is string type && !string.IsNullOrEmpty(type))
            {
                return [type];
            }

            if (obj is IEnumerable<string> types)
            {
                return types.ToArray();
            }
        }
        
        return null;
    }

    private string? GetFromContext(string headerName, string? defaultValue = null)
    {
        if (Context != null && Context.Bag.TryGetValue(headerName, out var val))
        {
            return val?.ToString() ?? defaultValue;
        }

        return defaultValue;
    } 

    /// <inheritdoc />
    public virtual TMessage MapToRequest(Message message)
    {
        var envelop = JsonSerializer.Deserialize<MassTransitMessageEnvelop<TMessage>>(message.Body.Bytes, JsonSerialisationOptions.Options);
        if (envelop == null)
        {
            throw new InvalidOperationException("It's not a MassTransit envelop message");
        }

        if (envelop.Message == null)
        {
            throw new InvalidOperationException("Message inside MassTransit envelop doesn't match the current type");
        }
        
        return envelop.Message;
    }
}
