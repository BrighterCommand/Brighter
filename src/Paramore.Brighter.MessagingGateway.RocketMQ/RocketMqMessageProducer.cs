using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NJsonSchema.Annotations;
using Org.Apache.Rocketmq;
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

        var rocketMessage = RocketMqMessagePublisher.CreateRocketMqMessage(
            message, mqPublication, delay, connection.TimerProvider);

        await producer.Send(rocketMessage);
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
