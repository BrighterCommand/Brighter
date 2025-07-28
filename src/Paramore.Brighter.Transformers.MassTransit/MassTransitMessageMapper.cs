using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Transforms.Attributes;

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
    private static readonly HostInfo? s_hostInfo = HostInfo.Create();
    private static readonly ConcurrentDictionary<Type, MassTransitMessageAttribute?> s_cachedTypes = new();

    /// <summary>
    /// The Masstransit content-type.
    /// </summary>
    public static ContentType MassTransitContentType { get; } = new("application/vnd.masstransit+json");

    /// <inheritdoc cref="IAmAMessageMapper{TRequest}.Context" />
    public IRequestContext? Context { get; set; }

    /// <inheritdoc />
    [CloudEvents(0)]
    public Task<Message> MapToMessageAsync(TMessage request, Publication publication,
        CancellationToken cancellationToken = default)
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
    public virtual Message MapToMessage(TMessage request, Publication publication)
    {
        var timestamp = DateTimeOffset.UtcNow;
        
        var defaultHeaders = publication.DefaultHeaders ?? new Dictionary<string, object>();
        var headers = defaultHeaders.Merge(Context.GetHeaders());
        
        var envelop = new MassTransitMessageEnvelop<TMessage>
        {
            ConversationId = GetConversationId(),
            CorrelationId = GetCorrelationId(),
            DestinationAddress = GetDestinationAddress(),
            ExpirationTime = GetExpirationTime(),
            FaultAddress = GetFaultAddress(),
            Headers = headers!,
            Host = s_hostInfo,
            InitiatorId = GetInitiatorId(),
            Message = request,
            MessageId = request.Id,
            MessageType = GetMessageType(),
            RequestId = GetRequestId(),
            ResponseAddress = GetResponseAddress(),
            SourceAddress = GetSourceAddress(),
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
                topic: publication.Topic!,
                partitionKey: Context.GetPartitionKey())
            {
                Bag = headers 
            },
            new MessageBody(JsonSerializer.SerializeToUtf8Bytes(envelop, JsonSerialisationOptions.Options),
                MassTransitContentType)
        );
    }

    private Id GetCorrelationId() => Context.GetIdFromBag(nameof(MessageHeader.CorrelationId), Id.Random)!;

    private Id? GetConversationId() => Context.GetIdFromBag(MassTransitHeaderNames.ConversationId);
    
    private Uri? GetDestinationAddress()
    {
        var response = Context.GetUriFromBag(MassTransitHeaderNames.DestinationAddress);
        if (response != null)
        {
            return response;
        }

        var address = GetMassTransitAttribute()?.DestinationAddress;
        return string.IsNullOrEmpty(address) ? null : new Uri(address, UriKind.RelativeOrAbsolute);
    }

    private DateTime? GetExpirationTime()
    {
        var val = Context.GetFromBag(MassTransitHeaderNames.ExpirationTime);
        return val switch
        {
            DateTimeOffset offset => offset.DateTime,
            DateTime dateTime => dateTime,
            _ => null
        };
    }

    private Uri? GetFaultAddress()
    {
        var response = Context.GetUriFromBag(MassTransitHeaderNames.FaultAddress);
        if (response != null)
        {
            return response;
        }

        var address = GetMassTransitAttribute()?.FaultAddress;
        return string.IsNullOrEmpty(address) ? null : new Uri(address, UriKind.RelativeOrAbsolute);
    }

    private Id? GetInitiatorId() => Context.GetIdFromBag(MassTransitHeaderNames.InitiatorId);

    private Id? GetRequestId() => Context.GetIdFromBag(MassTransitHeaderNames.RequestId);

    private Uri? GetResponseAddress()
    {
        var response = Context.GetUriFromBag(MassTransitHeaderNames.ResponseAddress);
        if (response != null)
        {
            return response;
        }

        var address = GetMassTransitAttribute()?.ResponseAddress;
        return string.IsNullOrEmpty(address) ? null : new Uri(address, UriKind.RelativeOrAbsolute);
    }

    private Uri? GetSourceAddress()
    {
        var source = Context.GetUriFromBag(MassTransitHeaderNames.SourceAddress);
        if (source != null)
        {
            return source;
        }
        
        var address = GetMassTransitAttribute()?.SourceAddress;
        return string.IsNullOrEmpty(address) ? null : new Uri(address, UriKind.RelativeOrAbsolute);
    }

    private string[]? GetMessageType()
    {
        var obj = Context.GetFromBag(MassTransitHeaderNames.MessageType);
        return obj switch
        {
            string type when !string.IsNullOrEmpty(type) => [type],
            IEnumerable<string> types => types.ToArray(),
            _ => GetMassTransitAttribute()?.MessageType
        };
    }

    /// <inheritdoc />
    public virtual TMessage MapToRequest(Message message)
    {
        var envelop =
            JsonSerializer.Deserialize<MassTransitMessageEnvelop<TMessage>>(message.Body.Bytes,
                JsonSerialisationOptions.Options);
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

    private static MassTransitMessageAttribute? GetMassTransitAttribute()
    {
        return s_cachedTypes.GetOrAdd(typeof(TMessage), type =>
        {
            var attribute = type.GetCustomAttribute<MassTransitMessageAttribute>();
            return attribute;
        });
    }
}
