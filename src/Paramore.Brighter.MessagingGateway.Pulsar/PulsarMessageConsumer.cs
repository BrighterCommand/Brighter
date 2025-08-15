using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using DotPulsar;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.Pulsar;

/// <summary>
/// A message consumer implementation for Apache Pulsar that supports both synchronous and asynchronous operations.
/// </summary>
/// <remarks>
/// This class handles message consumption from Pulsar topics including:
/// - Message acknowledgment
/// - Message rejection
/// - Queue purging
/// - Message receiving
/// - Message requeuing
/// 
/// It wraps a background Pulsar consumer and provides adapters to convert Pulsar-specific message formats
/// to Brighter's internal message format.
/// </remarks>
/// <param name="backgroundPulsarConsumer">The background consumer responsible for low-level Pulsar interactions</param>
public partial class PulsarMessageConsumer(PulsarBackgroundMessageConsumer backgroundPulsarConsumer) : IAmAMessageConsumerAsync, IAmAMessageConsumerSync
{
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<PulsarMessageConsumer>();
    
    /// <summary>
    /// Acknowledge a message so it won't be redelivered
    /// </summary>
    /// <param name="message">The message to acknowledge</param>
    /// <remarks>
    /// Uses the "ReceiptHandle" stored in the message's header bag to identify
    /// the Pulsar MessageId for acknowledgment
    /// </remarks>
    public void Acknowledge(Message message) 
        => BrighterAsyncContext.Run(async () => await AcknowledgeAsync(message));

    /// <summary>
    /// Asynchronously acknowledge a message so it won't be redelivered
    /// </summary>
    /// <param name="message">The message to acknowledge</param>
    /// <param name="cancellationToken">Optional cancellation token</param>
    /// <exception cref="Exception">Throws if acknowledgment fails</exception>
    public async Task AcknowledgeAsync(Message message, CancellationToken cancellationToken = default)
    { 
        if (!message.Header.Bag.TryGetValue("ReceiptHandle", out var receiptHandle))
        {
            return;
        }
        
        if (receiptHandle is not MessageId messageId)
        {
            return;
        }

        try
        {
            await backgroundPulsarConsumer.Consumer.Acknowledge(messageId, cancellationToken);
            Log.AcknowledgedMessage(s_logger, message.Id, messageId.ToString());
        }
        catch (Exception ex)
        {
            Log.ErrorAcknowledgingMessage(s_logger, ex, message.Id, messageId.ToString());
            throw;
        }
    }
    
    /// <summary>
    /// Reject a message so it can be redelivered
    /// </summary>
    /// <param name="message">The message to reject</param>
    /// <returns>True if message was rejected successfully</returns>
    public bool Reject(Message message)
        => BrighterAsyncContext.Run(async () => await RejectAsync(message));

    /// <summary>
    /// Asynchronously reject a message so it can be redelivered
    /// </summary>
    /// <param name="message">The message to reject</param>
    /// <param name="cancellationToken">Optional cancellation token</param>
    /// <returns>True if message was rejected successfully</returns>
    /// <remarks>
    /// In Pulsar, rejection is implemented as an acknowledgment since Pulsar
    /// doesn't have a native reject mechanism. This might cause the message to be
    /// permanently removed rather than redelivered depending on configuration.
    /// </remarks>
    public async Task<bool> RejectAsync(Message message, CancellationToken cancellationToken = default)
    {
        if (!message.Header.Bag.TryGetValue("ReceiptHandle", out var receiptHandle))
        {
            return false;
        }
        
        if (receiptHandle is not MessageId messageId)
        {
            return false;
        }

        try
        {
            Log.RejectingMessage(s_logger, message.Id, messageId.ToString());
            await backgroundPulsarConsumer.Consumer.Acknowledge(messageId, cancellationToken);
            return true;
        }
        catch (Exception exception)
        {
            Log.ErrorRejectingMessage(s_logger, exception, message.Id, messageId.ToString());
            throw;
        }
    }
    
    /// <summary>
    /// Purge all messages in the queue
    /// </summary>
    /// <remarks>
    /// Implemented by seeking to the latest message position, effectively
    /// skipping all pending messages
    /// </remarks>
    public void Purge() => BrighterAsyncContext.Run(async () => await PurgeAsync());

    /// <summary>
    /// Asynchronously purge all messages in the queue
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token</param>
    public async Task PurgeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Log.PurgingQueue(s_logger, backgroundPulsarConsumer.Consumer.SubscriptionName);
            await backgroundPulsarConsumer.Consumer.Seek(MessageId.Latest, cancellationToken);
            Log.PurgedQueue(s_logger, backgroundPulsarConsumer.Consumer.SubscriptionName);
        }
        catch (Exception exception)
        {
            Log.ErrorPurgingQueue(s_logger, exception, backgroundPulsarConsumer.Consumer.SubscriptionName);
            throw;
        }
    }
    
    /// <summary>
    /// Receive messages from the queue (synchronous wrapper)
    /// </summary>
    /// <param name="timeOut">Maximum time to wait for messages</param>
    /// <returns>Array of received messages</returns>
    public Message[] Receive(TimeSpan? timeOut = null) 
        => BrighterAsyncContext.Run(async () => await ReceiveAsync(timeOut));

    /// <inheritdoc />
    /// <summary>
    /// Asynchronously receive messages from Pulsar
    /// </summary>
    /// <param name="timeOut">Optional timeout for receiving messages</param>
    /// <param name="cancellationToken">Optional cancellation token</param>
    /// <returns>Array of received messages</returns>
    /// <remarks>
    /// Converts Pulsar messages to Brighter's internal message format:
    /// 1. Extracts Pulsar properties into message header bag
    /// 2. Maps Pulsar metadata to Brighter header fields
    /// 3. Handles timeouts by returning empty message array
    /// 
    /// Important mappings:
    /// - Pulsar MessageId → "ReceiptHandle" in header bag
    /// - Pulsar Properties → Custom header attributes
    /// - EventTime → Timestamp header
    /// - RedeliveryCount → HandledCount header
    /// </remarks>
    public async Task<Message[]> ReceiveAsync(TimeSpan? timeOut = null, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeOut.HasValue && timeOut.Value != TimeSpan.Zero)
        {
            cts.CancelAfter(timeOut.Value);
        }

        try
        {
            var pulsarMessage = await backgroundPulsarConsumer.Reader.ReadAsync(cts.Token);
            var bag = new Dictionary<string, object>();

            foreach (var pair in pulsarMessage.Properties)
            {
                bag[pair.Key] = pair.Value;
            }

            bag["ReceiptHandle"] = pulsarMessage.MessageId;
            bag[HeaderNames.SequenceId] = pulsarMessage.SequenceId;
            if (pulsarMessage.SchemaVersion != null)
            {
                bag[HeaderNames.SchemaVersion] = pulsarMessage.SchemaVersion;
            }

            var header = new MessageHeader(
                messageId: GetMessageId(pulsarMessage.Properties),
                topic: GetTopic(pulsarMessage.Properties, backgroundPulsarConsumer.Consumer.Topic),
                messageType: GetMessageType(pulsarMessage.Properties),
                contentType: GetContentType(pulsarMessage.Properties),
                partitionKey: GetPartitionKey(pulsarMessage.Key),
                timeStamp: pulsarMessage.EventTimeAsDateTimeOffset,
                correlationId: GetCorrelationId(pulsarMessage.Properties),
                handledCount: (int)pulsarMessage.RedeliveryCount,
                type: GetCloudEventType(pulsarMessage.Properties),
                source: GetSource(pulsarMessage.Properties),
                replyTo: GetReplyTo(pulsarMessage.Properties),
                subject: GetSubject(pulsarMessage.Properties),
                dataSchema: GetDataSchema(pulsarMessage.Properties),
                baggage: GetBaggage(pulsarMessage.Properties),
                traceParent: GetTraceParent(pulsarMessage.Properties),
                traceState: GetTraceState(pulsarMessage.Properties))
            {
                SpecVersion = GetSpecVersion(pulsarMessage.Properties),
                Bag = bag
            };

            return [new Message(header, new MessageBody(pulsarMessage.Data.ToArray()))];

        }
        catch (OperationCanceledException)
        {
            return [new Message()];
        }

        static Id GetMessageId(IReadOnlyDictionary<string, string> properties)
        {
            if (!properties.TryGetValue(HeaderNames.MessageId, out var id) || string.IsNullOrEmpty(id))
            {
                return Id.Random();
            }

            return Id.Create(id);
        }

        static RoutingKey GetTopic(IReadOnlyDictionary<string, string> properties, string defaultTopic)
        {
            if (!properties.TryGetValue(HeaderNames.Topic, out var topic) || string.IsNullOrEmpty(topic))
            {
                return new RoutingKey(defaultTopic);
            }

            return new RoutingKey(topic);
        }

        static MessageType GetMessageType(IReadOnlyDictionary<string, string> properties)
        {
            if (!properties.TryGetValue(HeaderNames.MessageType, out var messageType) || string.IsNullOrEmpty(messageType))
            {
                return MessageType.MT_EVENT;
            }
            
#if NETSTANDARD
            return (MessageType)Enum.Parse(typeof(MessageType), messageType);
#else
            return Enum.Parse<MessageType>(messageType);
#endif
        }

        static ContentType GetContentType(IReadOnlyDictionary<string, string> properties)
        {
            if (!properties.TryGetValue(HeaderNames.ContentType, out var contentType) || string.IsNullOrEmpty(contentType))
            {
                return new ContentType(MediaTypeNames.Text.Plain);
            }

            return new ContentType(contentType);
        }

        static PartitionKey? GetPartitionKey(string? partitionKey) 
            => string.IsNullOrEmpty(partitionKey) ? null : new PartitionKey(partitionKey!);
        
        static Id GetCorrelationId(IReadOnlyDictionary<string, string> properties)
        {
            if (!properties.TryGetValue(HeaderNames.CorrelationId, out var id) || string.IsNullOrEmpty(id))
            {
                return Id.Empty;
            }

            return Id.Create(id);
        }

        static string GetSpecVersion(IReadOnlyDictionary<string, string> properties)
        {
            if (!properties.TryGetValue(HeaderNames.SpecVersion, out var specVersion) 
                || string.IsNullOrEmpty(specVersion))
            {
                return MessageHeader.DefaultSpecVersion;
            }

            return specVersion;
        }
        
        static CloudEventsType GetCloudEventType(IReadOnlyDictionary<string, string> properties)
        {
            if (!properties.TryGetValue(HeaderNames.Type, out var type) 
                || string.IsNullOrEmpty(type))
            {
                return CloudEventsType.Empty;
            }

            return new CloudEventsType(type);
        }
        
        static Uri GetSource(IReadOnlyDictionary<string, string> properties)
        {
            if (!properties.TryGetValue(HeaderNames.Source, out var source) || !Uri.TryCreate(source, UriKind.RelativeOrAbsolute, out var sourceUri))
            {
                return new Uri(MessageHeader.DefaultSource);
            }

            return sourceUri;
        }

        static RoutingKey? GetReplyTo(IReadOnlyDictionary<string, string> properties)
        {
            if (!properties.TryGetValue(HeaderNames.ReplyTo, out var replyTo) || string.IsNullOrEmpty(replyTo))
            {
                return null;
            }

            return new RoutingKey(replyTo);
        }
        
        static string? GetSubject(IReadOnlyDictionary<string, string> properties)
        {
            properties.TryGetValue(HeaderNames.Subject, out var subject);
            return subject;
        }

        static Uri? GetDataSchema(IReadOnlyDictionary<string, string> properties)
        {
            if (!properties.TryGetValue(HeaderNames.DataSchema, out var source) || !Uri.TryCreate(source, UriKind.RelativeOrAbsolute, out var sourceUri))
            {
                return null;
            }

            return sourceUri;   
        }

        static Baggage GetBaggage(IReadOnlyDictionary<string, string> properties)
        {
            if (!properties.TryGetValue(HeaderNames.Baggage, out var baggage) || string.IsNullOrEmpty(baggage))
            {
                return new Baggage();
            }

            return Baggage.FromString(baggage);
        }

        static TraceParent? GetTraceParent(IReadOnlyDictionary<string, string> properties)
        {
            if (!properties.TryGetValue(HeaderNames.TraceParent, out var traceParent) || string.IsNullOrEmpty(traceParent))
            {
                return null;
            }

            return new TraceParent(traceParent);
        }

        static TraceState? GetTraceState(IReadOnlyDictionary<string, string> properties)
        {
            if (!properties.TryGetValue(HeaderNames.TraceState, out var traceState) || string.IsNullOrEmpty(traceState))
            {
                return null;
            }

            return new TraceState(traceState);
        }
    }

    /// <summary>
    /// Requeue a message with optional delay
    /// </summary>
    /// <param name="message">The message to requeue</param>
    /// <param name="delay">Optional delay before redelivery (not supported in Pulsar)</param>
    /// <returns>True if message was requeued successfully</returns>
    /// <remarks>
    /// In Pulsar, this triggers redelivery of the message through
    /// the redeliverUnacknowledgedMessages API
    /// </remarks>
    public bool Requeue(Message message, TimeSpan? delay = null) 
        => BrighterAsyncContext.Run(async () => await RequeueAsync(message, delay));

    /// <inheritdoc />
    /// <summary>
    /// Asynchronously requeue a message with optional delay
    /// </summary>
    /// <param name="message">The message to requeue</param>
    /// <param name="delay">Optional delay before redelivery (ignored in Pulsar implementation)</param>
    /// <param name="cancellationToken">Optional cancellation token</param>
    /// <returns>True if message was requeued successfully</returns> 
    public async Task<bool> RequeueAsync(Message message, TimeSpan? delay = null,
        CancellationToken cancellationToken = default)
    {
        if (!message.Header.Bag.TryGetValue("ReceiptHandle", out var receiptHandle))
        {
            return false;
        }
        
        var messageId = receiptHandle as MessageId;
        if (messageId! == null!)
        {
            return false;
        }

        try
        {

            Log.RejectingMessage(s_logger, message.Id, messageId.ToString());

            await backgroundPulsarConsumer.Consumer.RedeliverUnacknowledgedMessages([messageId], cancellationToken);
            
            Log.RequeuedMessage(s_logger, message.Id);
            return true;
        }
        catch (Exception ex)
        {
            Log.ErrorRejectingMessage(s_logger, ex, message.Id, messageId.ToString());
            throw;
        }
    }
    
    /// <summary>
    /// Asynchronously release consumer resources
    /// </summary>
    public ValueTask DisposeAsync()
    {
        backgroundPulsarConsumer.Stop();
        return new ValueTask();
    }
    
    /// <summary>
    /// Release consumer resources
    /// </summary>
    public void Dispose()
    {
        backgroundPulsarConsumer.Stop();
    }
    
    private static partial class Log
    {
        [LoggerMessage(LogLevel.Information, "PulsarMessageConsumer: Acknowledged message {MessageId} with receipt handle {ReceiptHandle} ")]
        public static partial void AcknowledgedMessage(ILogger logger, string messageId, string receiptHandle);
        
        [LoggerMessage(LogLevel.Error, "PulsarMessageConsumer: Error during acknowledged message {Id} with receipt handle {ReceiptHandle}")]
        public static partial void ErrorAcknowledgingMessage(ILogger logger, Exception exception, string id, string receiptHandle);
        
        
        [LoggerMessage(LogLevel.Information, "PulsarMessageConsumer: Rejecting the message {Id} with receipt handle {ReceiptHandle}")]
        public static partial void RejectingMessage(ILogger logger, string id, string? receiptHandle);

        [LoggerMessage(LogLevel.Error, "PulsarMessageConsumer: Error during rejecting the message {Id} with receipt handle {ReceiptHandle}")]
        public static partial void ErrorRejectingMessage(ILogger logger, Exception exception, string id, string? receiptHandle);
        
        
        [LoggerMessage(LogLevel.Information, "PulsarMessageConsumer: Purging the queue {ChannelName}")]
        public static partial void PurgingQueue(ILogger logger, string channelName);

        [LoggerMessage(LogLevel.Information, "PulsarMessageConsumer: Purged the queue {ChannelName}")]
        public static partial void PurgedQueue(ILogger logger, string channelName);

        [LoggerMessage(LogLevel.Error, "PulsarMessageConsumer: Error purging queue {ChannelName}")]
        public static partial void ErrorPurgingQueue(ILogger logger, Exception exception, string channelName);
        
        
        [LoggerMessage(LogLevel.Information, "PulsarMessageConsumer: re-queueing the message {Id}")]
        public static partial void RequeueingMessage(ILogger logger, string id);

        [LoggerMessage(LogLevel.Information, "PulsarMessageConsumer: re-queued the message {Id}")]
        public static partial void RequeuedMessage(ILogger logger, string id);

        [LoggerMessage(LogLevel.Error, "SqsMessageConsumer: Error during re-queueing the message {Id} with receipt handle {ReceiptHandle} on the queue {ChannelName}")]
        public static partial void ErrorRequeueingMessage(ILogger logger, Exception exception, string id, string? receiptHandle, string channelName);
    }
}
