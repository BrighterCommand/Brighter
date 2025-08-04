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
/// A message transform that wraps messages in a MassTransit-compatible envelope for interoperability.
/// </summary>
/// <remarks>
/// <para>
/// This transform integrates Brighter with MassTransit by wrapping messages in a <see cref="MassTransitMessageEnvelop{TMessage}"/> 
/// structure during message publication. It ensures compatibility with MassTransit's default envelope format, including required 
/// metadata like CorrelationId, HostInfo, and routing addresses.
/// </para>
/// <para>
/// <strong>Performance Note:</strong> Prefer using <see cref="MassTransitMessageMapper{TMessage}"/> for better efficiency. 
/// This transform dynamically manipulates JSON payloads, which incurs higher overhead compared to the strongly-typed mapping 
/// approach in <see cref="MassTransitMessageMapper{TMessage}"/>. 
/// </para>
/// <para>
/// <strong>Key Features:</strong>
/// - Wraps messages in MassTransit's expected envelope format
/// - Sets standard MassTransit headers (DestinationAddress, SourceAddress, etc.)
/// - Serializes messages with <c>application/vnd.masstransit+json</c> content type
/// - Supports both synchronous and asynchronous transformation
/// </para>
/// <para>
/// <strong>Configuration:</strong>
/// - Parameters like <c>destinationAddress</c>, <c>responseAddress</c>, and <c>messageType</c> can be set via attribute constructor arguments
/// - Missing values are sourced from message context or defaults (e.g., publication source for SourceAddress)
/// - Preserves existing message headers in the envelope's <c>Headers</c> property
/// </para>
/// </remarks>
public class MassTransitTransform : IAmAMessageTransform, IAmAMessageTransformAsync
{
    private static readonly HostInfo?  s_hostInfo = HostInfo.Create();
    private static readonly ContentType s_massTransitContentType = new("application/vnd.masstransit+json");

    private Uri? _destinationAddress; 
    private Uri? _faultAddress;
    private Uri? _responseAddress;
    private Uri? _sourceAddress;
    private string[]? _messageType;
    
    /// <inheritdoc cref="IAmAMessageTransform.Context"/>
    public IRequestContext? Context { get; set; }
    
    /// <inheritdoc cref="IAmAMessageTransform.InitializeWrapFromAttributeParams"/>
    public void InitializeWrapFromAttributeParams(params object?[] initializerList)
    {
        if (initializerList[0] is string destinationAddress)
        {
            _destinationAddress = new Uri(destinationAddress, UriKind.RelativeOrAbsolute);
        }

        if (initializerList[1] is string faultAddress)
        {
            _faultAddress = new Uri(faultAddress, UriKind.RelativeOrAbsolute);
        }
        
        if (initializerList[2] is string responseAddress)
        {
            _responseAddress = new Uri(responseAddress, UriKind.RelativeOrAbsolute);
        }

        if (initializerList[3] is string sourceAddress)
        {
            _sourceAddress = new Uri(sourceAddress, UriKind.RelativeOrAbsolute);
        }

        if (initializerList[4] is string[] messageType)
        {
            _messageType = messageType;
        }
    }

    /// <inheritdoc cref="IAmAMessageTransform.InitializeUnwrapFromAttributeParams"/>
    public void InitializeUnwrapFromAttributeParams(params object?[] initializerList)
    {
    }

    /// <inheritdoc />
    public Task<Message> WrapAsync(Message message, Publication publication, CancellationToken cancellationToken)
    {
        return Task.FromResult(Wrap(message, publication));
    }

    /// <inheritdoc />
    public Task<Message> UnwrapAsync(Message message, CancellationToken cancellationToken)
    {
        return Task.FromResult(Unwrap(message));
    }

    /// <inheritdoc />
    public Message Wrap(Message message, Publication publication)
    {
        var data = JsonSerializer.Deserialize<JsonElement>(message.Body.Value, JsonSerialisationOptions.Options);

        var envelop = new MassTransitMessageEnvelop<JsonElement>
        {
            ConversationId = GetConversationId(),
            CorrelationId = message.Header.CorrelationId,
            DestinationAddress = GetDestinationAddress(), 
            ExpirationTime = GetExpirationTime(), 
            FaultAddress = GetFaultAddress(), 
            Headers = message.Header.Bag!,
            Host = s_hostInfo,
            InitiatorId = GetInitiatorId(), 
            Message = data,
            MessageId = message.Header.MessageId,
            MessageType = GetMessageType(),
            RequestId = GetRequestId(),
            ResponseAddress = GetResponseAddress(message),
            SourceAddress = GetSourceAddress(),
            SentTime = GetSentTime(message)
        };

        message.Header.ContentType = s_massTransitContentType;
        message.Body = new MessageBody(JsonSerializer.SerializeToUtf8Bytes(envelop, JsonSerialisationOptions.Options), s_massTransitContentType);
        return message;
    }

    private Id? GetConversationId() => Context.GetIdFromBag(MassTransitHeaderNames.ConversationId);

    private Uri? GetDestinationAddress() => _destinationAddress ?? Context.GetUriFromBag(MassTransitHeaderNames.DestinationAddress);

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
    private Uri? GetFaultAddress() => _faultAddress ?? Context.GetUriFromBag(MassTransitHeaderNames.FaultAddress);

    private Id? GetInitiatorId() => Context.GetIdFromBag(MassTransitHeaderNames.InitiatorId);

    private Id? GetRequestId() => Context.GetIdFromBag(MassTransitHeaderNames.RequestId);

    private Uri? GetResponseAddress(Message message)
    {
        if (_responseAddress != null) 
        { 
            return _responseAddress;
        }
        
        if (!RoutingKey.IsNullOrEmpty(message.Header.ReplyTo)) 
        { 
            return new Uri(message.Header.ReplyTo!, UriKind.RelativeOrAbsolute); 
        }
        
        return Context.GetUriFromBag(MassTransitHeaderNames.ResponseAddress);
    }
    
    private Uri? GetSourceAddress() => _sourceAddress ?? Context.GetUriFromBag(MassTransitHeaderNames.SourceAddress);

    private string[]? GetMessageType()
    {
        if (_messageType is { Length: > 0 })
        {
            return _messageType;
        }

        var obj = Context.GetFromBag(MassTransitHeaderNames.MessageType);
        return obj switch
        {
            string type when !string.IsNullOrEmpty(type) => [type],
            IEnumerable<string> types => types.ToArray(),
            _ => null
        };
    }

    private static DateTime GetSentTime(Message message) => message.Header.TimeStamp == DateTimeOffset.MinValue ? DateTime.UtcNow : message.Header.TimeStamp.DateTime;


    /// <inheritdoc />
    public Message Unwrap(Message message)
    {
        var envelop = JsonSerializer.Deserialize<MassTransitMessageEnvelop<JsonElement>>(message.Body.Bytes, JsonSerialisationOptions.Options);

        if (envelop != null)
        {
            var messageId = Id.Random();
            if (!Id.IsNullOrEmpty(envelop.MessageId))
            {
               messageId = envelop.MessageId!;
            }
            
            var timestamp = DateTimeOffset.UtcNow;
            if (envelop.SentTime != null)
            {
                timestamp = envelop.SentTime.Value;
            }
            
            if (!Id.IsNullOrEmpty(envelop.CorrelationId))
            {
                message.Header.CorrelationId = envelop.CorrelationId!;
            }

            var bag = message.Header.Bag;
            if (envelop.Headers is { Count: > 0 })
            {
                bag = new Dictionary<string, object>(bag);
                foreach (KeyValuePair<string, object?> obj in  envelop.Headers)
                {
                    bag[obj.Key] = obj.Value!;
                }    
            }

            return new Message(
                new MessageHeader(
                    messageId,
                    message.Header.Topic,
                    message.Header.MessageType,
                    source: message.Header.Source,
                    type: message.Header.Type,
                    timeStamp: timestamp!,
                    correlationId: message.Header.CorrelationId, 
                    partitionKey: message.Header.PartitionKey,
                    dataSchema: message.Header.DataSchema,
                    subject: message.Header.Subject,
                    handledCount: message.Header.HandledCount,
                    delayed: message.Header.Delayed,
                    traceParent: message.Header.TraceParent,
                    traceState: message.Header.TraceState,
                    baggage:  message.Header.Baggage)
                {
                    Bag = bag
                },
                new MessageBody(JsonSerializer.SerializeToUtf8Bytes(envelop.Message, JsonSerialisationOptions.Options))
            );
        }

        return message;
    }

    public void Dispose()
    {
    }
}
