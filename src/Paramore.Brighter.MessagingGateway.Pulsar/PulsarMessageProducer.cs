using System;
using System.Buffers;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DotPulsar;
using DotPulsar.Abstractions;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.Pulsar;

/// <summary>
/// Implements a message producer for Apache Pulsar within the Brighter message processing framework.
/// Handles message publication with optional delay and metadata management.
/// </summary>
/// <param name="producer">The underlying Pulsar producer instance</param>
/// <param name="publication">Configuration for Pulsar message publication</param>
/// <param name="time">Time provider for delayed message scheduling</param>
/// <param name="instrumentation">Options controlling instrumentation behavior</param>
/// <remarks>
/// This producer bridges Brighter's message publication model with Pulsar's producer API:
/// - Converts Brighter <see cref="Message"/> to Pulsar's message format
/// - Supports delayed message delivery
/// - Manages cloud events metadata and standard headers
/// - Implements proper resource disposal patterns
/// </remarks>
public class PulsarMessageProducer(IProducer<ReadOnlySequence<byte>> producer, 
    PulsarPublication publication,
    TimeProvider time,
    InstrumentationOptions instrumentation) : IAmAMessageProducerAsync, IAmAMessageProducerSync
{
    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await producer.DisposeAsync();

    /// <summary>
    /// The <see cref="Publication "/>that this Producer is for.
    /// </summary>
    public Publication Publication => publication;
    
    /// <summary>
    /// Allows us to set a <see cref="BrighterTracer"/> to let a Producer participate in our telemetry
    /// </summary>
    public Activity? Span { get; set; }
    
    /// <summary>
    /// The external message scheduler
    /// </summary>
    public IAmAMessageScheduler? Scheduler { get; set; }
    
    /// <summary>
    /// Sends the specified message.
    /// </summary>
    /// <param name="message">The message.</param>
    public void Send(Message message) 
        => SendWithDelay(message, TimeSpan.Zero);

    /// <summary>
    /// Sends the specified message.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">A cancellation token to end the operation</param>
    public Task SendAsync(Message message, CancellationToken cancellationToken = default) 
        => SendWithDelayAsync(message, TimeSpan.Zero, cancellationToken);
    
    /// <summary>
    /// Send the specified message with specified delay
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="delay">Delay delivery of the message. 0 is no delay. Defaults to 0</param>
    public void SendWithDelay(Message message, TimeSpan? delay)
        => BrighterAsyncContext.Run(async() => await SendWithDelayAsync(message, delay));

    
    /// <summary>
    /// Send the specified message with specified delay
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="delay">Delay to the delivery of the message. 0 is no delay. Defaults to 0</param>
    /// <param name="cancellationToken">A cancellation token to end the operation</param>
    public async Task SendWithDelayAsync(Message message, TimeSpan? delay, CancellationToken cancellationToken = default)
    {
        BrighterTracer.WriteProducerEvent(Span, MessagingSystem.Pulsar, message, instrumentation);
        await producer.Send(CreateMessageMetadata(message, delay), 
            new ReadOnlySequence<byte>(message.Body.Bytes), 
            cancellationToken);
    }

    private MessageMetadata CreateMessageMetadata(Message message, TimeSpan? delay)
    {
        var metadata = new MessageMetadata
        {
            Key = PartitionKey.IsNullOrEmpty(message.Header.PartitionKey) ? null : message.Header.PartitionKey.Value,
            EventTimeAsDateTimeOffset = message.Header.TimeStamp,
            SchemaVersion = publication.SchemaVersion,
            SequenceId = publication.GenerateSequenceId(message),
        };
        
        if (delay.HasValue && delay.Value != TimeSpan.Zero)
        {
            metadata.DeliverAtTimeAsDateTimeOffset = time.GetUtcNow() + delay.Value; 
        }
        
        metadata[HeaderNames.ContentType] = message.Header.ContentType.ToString();
        metadata[HeaderNames.CorrelationId] = message.Header.CorrelationId;
        metadata[HeaderNames.MessageType] = message.Header.MessageType.ToString();
        metadata[HeaderNames.MessageId] = message.Header.MessageId;
        metadata[HeaderNames.SpecVersion] = message.Header.SpecVersion;
        metadata[HeaderNames.Type] = message.Header.Type;
        metadata[HeaderNames.Time] = message.Header.TimeStamp.ToRfc3339();
        metadata[HeaderNames.Topic] = message.Header.Topic;
        metadata[HeaderNames.Source] = message.Header.Source.ToString();
        metadata[HeaderNames.Baggage] = message.Header.Baggage.ToString();
        
        if (!RoutingKey.IsNullOrEmpty(message.Header.ReplyTo))
        {
            metadata[HeaderNames.ReplyTo] = message.Header.ReplyTo;
        }
        
        if (!string.IsNullOrEmpty(message.Header.Subject))
        {
            metadata[HeaderNames.Subject] = message.Header.Subject;
        }

        if (message.Header.DataSchema != null)
        {
            metadata[HeaderNames.DataSchema] = message.Header.DataSchema.ToString();
        }
        
        if (!TraceParent.IsNullOrEmpty(message.Header.TraceParent))
        {
            metadata[HeaderNames.TraceParent] = message.Header.TraceParent;
        }
        
        if (!TraceState.IsNullOrEmpty(message.Header.TraceState))
        {
            metadata[HeaderNames.TraceState] = message.Header.TraceState;
        }

        foreach (var pair in  message.Header.Bag)
        {
            metadata[pair.Key] = pair.Value.ToString();
        }

        return metadata;
    }

    /// <inheritdoc />
    public void Dispose() => DisposeAsync().GetAwaiter().GetResult();
}
