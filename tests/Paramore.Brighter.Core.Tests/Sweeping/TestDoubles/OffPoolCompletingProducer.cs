using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;

/// <summary>
/// An async producer that completes each send the way Confluent's <c>ProduceAsync</c> does: a dedicated
/// delivery thread, not a pool thread, writes the message to the bus and then completes a
/// <see cref="TaskCompletionSource"/> created with <see cref="TaskCreationOptions.RunContinuationsAsynchronously"/>.
/// So the continuation after the producer's own <c>await</c> is never run inline by the delivery thread.
/// </summary>
public sealed class OffPoolCompletingProducer : IAmAMessageProducerAsync
{
    private readonly InternalBus _bus;
    private readonly BlockingCollection<(Message Message, TaskCompletionSource Delivered)> _deliveries = new();
    private readonly Thread _deliveryThread;

    public OffPoolCompletingProducer(InternalBus bus, Publication publication)
    {
        _bus = bus;
        Publication = publication;
        _deliveryThread = new Thread(Deliver) { IsBackground = true, Name = "Off-pool delivery" };
        _deliveryThread.Start();
    }

    public Publication Publication { get; }
    public Activity? Span { get; set; }
    public IAmAMessageScheduler? Scheduler { get; set; }

    // Awaits the delivery inside the producer, as KafkaMessagePublisher awaits ProduceAsync: a plain await, so
    // its continuation follows the caller's context. The mediator's own awaits use ConfigureAwait(false).
    public async Task SendAsync(Message message, CancellationToken cancellationToken = default)
    {
        var delivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _deliveries.Add((message, delivered), cancellationToken);
        await delivered.Task;
    }

    public Task SendWithDelayAsync(Message message, TimeSpan? delay, CancellationToken cancellationToken = default)
        => SendAsync(message, cancellationToken);

    public ValueTask DisposeAsync()
    {
        _deliveries.CompleteAdding();
        _deliveryThread.Join(TimeSpan.FromSeconds(5));
        return ValueTask.CompletedTask;
    }

    private void Deliver()
    {
        foreach (var (message, delivered) in _deliveries.GetConsumingEnumerable())
        {
            _bus.Enqueue(message);
            delivered.SetResult();
        }
    }
}
