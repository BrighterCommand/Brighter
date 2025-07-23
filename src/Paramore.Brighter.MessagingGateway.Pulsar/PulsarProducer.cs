using System;
using System.Buffers;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DotPulsar;
using DotPulsar.Abstractions;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.MessagingGateway.Pulsar;

public class PulsarProducer(IProducer<ReadOnlySequence<byte>> producer, 
    PulsarPublication publication,
    TimeProvider time,
    InstrumentationOptions instrumentation) : IAmAMessageProducerAsync
{
    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await producer.DisposeAsync();

    /// <inheritdoc />
    public Publication Publication => publication;
    
    /// <inheritdoc />
    public Activity? Span { get; set; }
    
    /// <inheritdoc />
    public IAmAMessageScheduler? Scheduler { get; set; }
    
    /// <inheritdoc />
    public Task SendAsync(Message message, CancellationToken cancellationToken = default) 
        => SendWithDelayAsync(message, TimeSpan.Zero, cancellationToken);

    /// <inheritdoc />
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
}
