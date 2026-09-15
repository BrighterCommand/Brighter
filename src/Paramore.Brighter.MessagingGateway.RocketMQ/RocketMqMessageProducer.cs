using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NJsonSchema.Annotations;
using Org.Apache.Rocketmq;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.RocketMQ;

/// <summary>
/// RocketMQ message producer implementation for Brighter.
/// Integrates RocketMQ's producer group pattern and transactional message support.
/// </summary>
public class RocketMqMessageProducer(
    RocketMessagingGatewayConnection connection,
    Producer producer,
    RocketMqPublication mqPublication,
    InstrumentationOptions instrumentation = InstrumentationOptions.All)
    : IAmAMessageProducerSync, IAmAMessageProducerAsync
{
    /// <inheritdoc />
    public Publication Publication => mqPublication;

    /// <inheritdoc />
    public Activity? Span { get; set; }

    /// <inheritdoc />
    public IAmAMessageScheduler? Scheduler { get; set; }

    /// <inheritdoc />
    public void Send(Message message)
    {
        SendWithDelay(message, TimeSpan.Zero);
    }

    /// <inheritdoc />
    public void SendWithDelay(Message message, TimeSpan? delay)
    {
        BrighterAsyncContext.Run(() => SendWithDelayAsync(message, delay, false));
    }

    /// <inheritdoc />
    public Task SendAsync(Message message, CancellationToken cancellationToken = default)
    {
        return SendWithDelayAsync(message, TimeSpan.Zero, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SendWithDelayAsync(Message message, TimeSpan? delay, CancellationToken cancellationToken = default)
    {
        await SendWithDelayAsync(message, delay, true, cancellationToken);
    }

    private async Task SendWithDelayAsync(Message message, TimeSpan? delay, bool useAsyncScheduler, CancellationToken cancellationToken = default)
    {
        if (delay.HasValue && delay.Value != TimeSpan.Zero && mqPublication.TopicType != TopicType.Delay)
        {
            if (useAsyncScheduler)
            {
                var schedulerAsync = (IAmAMessageSchedulerAsync)Scheduler!;
                await schedulerAsync.ScheduleAsync(message, delay.Value, cancellationToken);
                return;
            }
            
            var schedulerSync = (IAmAMessageSchedulerSync)Scheduler!;
            schedulerSync.Schedule(message, delay.Value);
            return;
        }
        
        BrighterTracer.WriteProducerEvent(Span, MessagingSystem.RocketMQ, message, instrumentation);
        var builder = new Org.Apache.Rocketmq.Message.Builder()
            .SetBody(message.Body.ToByteArray())
            .SetTopic(mqPublication.Topic!.Value);

        AddHeaderProperties(builder, message.Id, message.Header);

        if (mqPublication.TopicType == TopicType.Delay || delay.HasValue && delay.Value != TimeSpan.Zero)
        {
            delay ??= TimeSpan.Zero;
            builder
                .SetDeliveryTimestamp(connection.TimerProvider.GetUtcNow().Add(delay.Value).UtcDateTime);
        }
        
        if (mqPublication.TopicType == TopicType.Fifo || !PartitionKey.IsNullOrEmpty(message.Header.PartitionKey))
        {
            builder.SetMessageGroup(message.Header.PartitionKey.Value);
        }
        
        foreach (var (key, val) in message.Header.Bag
                     .Where(x => x.Key != HeaderNames.Keys
                                 && x.Key != HeaderNames.Tag
                                 && !MessageHeader.IsLocalHeader(x.Key)))
        {
            builder.AddProperty(key, val.ToString());
        }

        if (message.Header.Bag.TryGetValue(HeaderNames.Keys, out var keys))
        {
            if (keys is string[] keysArray)
            {
                builder.SetKeys(keysArray);
            }
            else if (keys is string keyString)
            {
                builder.SetKeys(keyString);
            }
        }
        else
        {
            builder.SetKeys(message.Id);
        }
        
        if (message.Header.Bag.TryGetValue(HeaderNames.Tag, out var tag) && tag is string tagString)
        {
            builder.SetTag(tagString);
        }
        else if (!string.IsNullOrEmpty(mqPublication.Tag))
        {
            builder.SetTag(mqPublication.Tag);
        }
        
        await producer.Send(builder.Build());
    }
    
    /// <inheritdoc />
    public void Dispose()
    {
    }
    
    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return new ValueTask();
    }

    /// <summary>
    /// Copies <paramref name="header"/> onto <paramref name="builder"/> as RocketMQ properties.
    /// </summary>
    /// <remarks>
    /// Extracted from the send path so the guards below can be tested without a broker. RocketMQ's
    /// <c>AddProperty</c> rejects an empty value with <see cref="ArgumentException"/>, so every
    /// optional header has to be checked before it is written - a header that simply is not set
    /// would otherwise fail the send rather than be omitted.
    /// </remarks>
    /// <param name="builder">The RocketMQ message under construction.</param>
    /// <param name="messageId">The Brighter message id.</param>
    /// <param name="header">The header whose values are copied.</param>
    internal static void AddHeaderProperties(
        Org.Apache.Rocketmq.Message.Builder builder, Id messageId, MessageHeader header)
    {
        builder.AddProperty(HeaderNames.MessageId, messageId)
            .AddProperty(HeaderNames.Topic, header.Topic.Value)
            .AddProperty(HeaderNames.HandledCount, header.HandledCount.ToString())
            .AddProperty(HeaderNames.MessageType, header.MessageType.ToString())
            .AddProperty(HeaderNames.TimeStamp, header.TimeStamp.ToRfc3339())
            .AddProperty(HeaderNames.Source, header.Source.ToString())
            .AddProperty(HeaderNames.SpecVersion, header.SpecVersion);

        var baggage = header.Baggage.ToString();
        if (!string.IsNullOrEmpty(baggage))
        {
            builder.AddProperty(HeaderNames.Baggage, baggage);
        }

        if (header.Type != CloudEventsType.Empty)
        {
            builder.AddProperty(HeaderNames.Type, header.Type);
        }
        
        if (!string.IsNullOrEmpty(header.Subject))
        {
            builder.AddProperty(HeaderNames.Subject, header.Subject);
        }

        if (header.DataSchema != null)
        {
            builder.AddProperty(HeaderNames.DataSchema, header.DataSchema.ToString());
        }

        builder.AddProperty(HeaderNames.ContentType, header.ContentType.ToString());
        builder.AddProperty(HeaderNames.DataContentType, header.ContentType.ToString());

        if (!string.IsNullOrEmpty(header.CorrelationId))
        {
            builder.AddProperty(HeaderNames.CorrelationId, header.CorrelationId);
        }
        
        if (!RoutingKey.IsNullOrEmpty(header.ReplyTo))
        {
            builder.AddProperty(HeaderNames.ReplyTo, header.ReplyTo);
        }

        if (!string.IsNullOrEmpty(header.DataRef))
        {
            builder.AddProperty(HeaderNames.DataRef, header.DataRef);
        }
        
        if (!TraceParent.IsNullOrEmpty(header.TraceParent))
        {
            builder.AddProperty(HeaderNames.TraceParent, header.TraceParent.Value);
        }

        if (!TraceState.IsNullOrEmpty(header.TraceState))
        {
            builder.AddProperty(HeaderNames.TraceState, header.TraceState.Value);
        }
    }

}
