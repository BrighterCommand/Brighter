using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Mime;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.JsonConverters;

namespace Paramore.Brighter.Transformers.MassTransit;

public class MassTransitTransform : IAmAMessageTransform, IAmAMessageTransformAsync
{
    private static HostInfo?  s_hostInfo;
    private static readonly ContentType s_massTransitContentType = new("application/vnd.masstransit+json");

    private string? _destinationAddress; 
    private string? _faultAddress;
    private string? _responseAddress;
    private string? _sourceAddress;
    private string[]? _messageType;
    
    public IRequestContext? Context { get; set; }
    
    public void InitializeWrapFromAttributeParams(params object?[] initializerList)
    {
        if (initializerList[0] is string destinationAddress)
        {
            _destinationAddress = destinationAddress;
        }

        if (initializerList[1] is string faultAddress)
        {
            _faultAddress = faultAddress;
        }
        
        if (initializerList[2] is string responseAddress)
        {
            _responseAddress = responseAddress;
        }

        if (initializerList[3] is string sourceAddress)
        {
            _sourceAddress = sourceAddress;
        }

        if (initializerList[4] is string[] messageType)
        {
            _messageType = messageType;
        }
    }

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
            ConversationId = GetConversationId(message),
            CorrelationId = message.Header.CorrelationId,
            DestinationAddress = GetDestinationAddress(message), 
            ExpirationTime = GetExpirationTime(message), 
            FaultAddress = GetFaultAddress(message), 
            Headers = message.Header.Bag
                .Where(x => !x.Key.StartsWith("MT-"))
                .ToDictionary(x => x.Key, x => x.Value)!,
            Host = GetHostInfo(),
            InitiatorId = GetInitiatorId(message), 
            Message = data,
            MessageId = message.Header.MessageId,
            MessageType = _messageType,
            RequestId = GetRequestId(message),
            ResponseAddress = GetResponseAddress(message),
            SourceAddress = GetSourceAddress(message),
            SentTime = message.Header.TimeStamp.DateTime
        };

        message.Header.ContentType = s_massTransitContentType;
        message.Body = new MessageBody(JsonSerializer.SerializeToUtf8Bytes(envelop, JsonSerialisationOptions.Options), s_massTransitContentType);
        return message;
    }

    private string? GetConversationId(Message message) => Get(message, MassTransitHeaderNames.ConversationId);
    
    private string? GetDestinationAddress(Message message)
    {
        if(!string.IsNullOrEmpty(_destinationAddress)) 
        { 
            return _destinationAddress;
        }
        
        return Get(message, MassTransitHeaderNames.DestinationAddress);
    }
    
    private DateTime? GetExpirationTime(Message message)
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
        
        if (message.Header.Bag.TryGetValue(MassTransitHeaderNames.ExpirationTime, out val))
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
    private string? GetFaultAddress(Message message)
    {
        if (!string.IsNullOrEmpty(_faultAddress)) 
        { 
            return _faultAddress;
        }
        
        return Get(message, MassTransitHeaderNames.FaultAddress);
    }
    private string? GetInitiatorId(Message message) => Get(message, MassTransitHeaderNames.InitiatorId);
    
    private string? GetRequestId(Message message) => Get(message, MassTransitHeaderNames.RequestId);
    
    private string? GetResponseAddress(Message message)
    {
        if (!string.IsNullOrEmpty(_responseAddress)) 
        { 
            return _responseAddress;
        }
        
        if (message.Header.ReplyTo != null) 
        { 
            return message.Header.ReplyTo!; 
        }
        
        return Get(message, MassTransitHeaderNames.ResponseAddress);
    }
    
    private string? GetSourceAddress(Message message)
    {
        if (!string.IsNullOrEmpty(_sourceAddress)) 
        { 
            return _sourceAddress;
        }
        
        return Get(message, MassTransitHeaderNames.SourceAddress);
    }

    private string? Get(Message message, string headerName)
    {
        if (Context != null
            && Context.Bag.TryGetValue(headerName, out var val))
        {
            return val?.ToString();
        }
        
        if (message.Header.Bag.TryGetValue(headerName, out val))
        {
            return val?.ToString();
        }

        return null;
    }

    /// <inheritdoc />
    public Message Unwrap(Message message)
    {
        var envelop = JsonSerializer.Deserialize<MassTransitMessageEnvelop<JsonElement>>(message.Body.Bytes, JsonSerialisationOptions.Options);

        if (envelop != null)
        {
            message.Body = new MessageBody(JsonSerializer.SerializeToUtf8Bytes(envelop.Message, JsonSerialisationOptions.Options));
        }

        return message;
    }

    private static HostInfo GetHostInfo()
    {
        if (s_hostInfo != null)
        {
            return s_hostInfo;
        }

        var process = Process.GetCurrentProcess();

        return s_hostInfo = new HostInfo
        {
            MachineName = Environment.MachineName,
            ProcessName = process.ProcessName,
            ProcessId = process.Id,
            OperatingSystemVersion = Environment.OSVersion.VersionString
        };
    }
    
    public void Dispose()
    {
    }
}
