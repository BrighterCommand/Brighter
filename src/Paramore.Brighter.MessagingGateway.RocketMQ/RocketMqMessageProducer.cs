using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        => SendWithDelay(message, TimeSpan.Zero);

    /// <inheritdoc />
    public void SendWithDelay(Message message, TimeSpan? delay)
        => BrighterAsyncContext.Run(async () => await SendWithDelayAsync(message, delay));
    

    /// <inheritdoc />
    public Task SendAsync(Message message, CancellationToken cancellationToken = default)
        => SendWithDelayAsync(message, TimeSpan.Zero, cancellationToken);

    /// <inheritdoc />
    public async Task SendWithDelayAsync(Message message, TimeSpan? delay, CancellationToken cancellationToken = default)
    {
        var builder = new Org.Apache.Rocketmq.Message.Builder()
            .SetBody(message.Body.Bytes)
            .SetTopic(mqPublication.Topic!.Value);

        builder.AddProperty(HeaderNames.MessageId, message.Id)
            .AddProperty(HeaderNames.Topic, message.Header.Topic.Value)
            .AddProperty(HeaderNames.HandledCount, message.Header.HandledCount.ToString())
            .AddProperty(HeaderNames.MessageType, message.Header.MessageType.ToString())
            .AddProperty(HeaderNames.TimeStamp, message.Header.TimeStamp.ToRfc3339())
            .AddProperty(HeaderNames.Source, message.Header.Source.ToString())
            .AddProperty(HeaderNames.SpecVersion, message.Header.SpecVersion);

        if (message.Header.Type != CloudEventsType.Empty)
        {
            builder.AddProperty(HeaderNames.Type, message.Header.Type);
        }
        
        if (!string.IsNullOrEmpty(message.Header.Subject))
        {
            builder.AddProperty(HeaderNames.Subject, message.Header.Subject);
        }

        if (message.Header.DataSchema != null)
        {
            builder.AddProperty(HeaderNames.DataSchema, message.Header.DataSchema.ToString());
        }
        
        if (!string.IsNullOrEmpty(message.Header.Type))
        {
            builder.AddProperty(HeaderNames.Type, message.Header.Type);
        }

        builder.AddProperty(HeaderNames.ContentType, message.Header.ContentType.ToString());
        builder.AddProperty(HeaderNames.DataContentType, message.Header.ContentType.ToString());

        if (!string.IsNullOrEmpty(message.Header.CorrelationId))
        {
            builder.AddProperty(HeaderNames.CorrelationId, message.Header.CorrelationId);
        }
        
        if (!RoutingKey.IsNullOrEmpty(message.Header.ReplyTo))
        {
            builder.AddProperty(HeaderNames.ReplyTo, message.Header.ReplyTo);
        }

        if (!string.IsNullOrEmpty(message.Header.DataRef))
        {
            builder.AddProperty(HeaderNames.DataRef, message.Header.DataRef);
        }
        
        if (delay.HasValue && delay.Value != TimeSpan.Zero)
        {
            builder
                .SetDeliveryTimestamp(connection.TimerProvider.GetUtcNow().Add(delay.Value).UtcDateTime);
        }
        
        if (!PartitionKey.IsNullOrEmpty(message.Header.PartitionKey))
        {
            builder.SetMessageGroup(message.Header.PartitionKey);
        }
        
        foreach (var (key, val) in message.Header.Bag
                     .Where(x => x.Key != HeaderNames.Keys && x.Key != HeaderNames.Tag))
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
        BrighterTracer.WriteProducerEvent(Span, MessagingSystem.RocketMQ, message, instrumentation);
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
}
