using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;

/// <summary>
/// An async producer that completes each send from its own delivery thread, as Confluent's <c>ProduceAsync</c>
/// does (a <see cref="TaskCreationOptions.RunContinuationsAsynchronously"/> task awaited plainly inside the
/// producer), and fails its first send, so a retry pipeline has to retry it.
/// </summary>
public sealed class FailsOnceOffPoolProducer : IAmAMessageProducerAsync
{
    private readonly InternalBus _bus;
    private readonly BlockingCollection<(Message Message, TaskCompletionSource Delivered)> _deliveries = new();
    private readonly Thread _deliveryThread;
    private int _sends;

    public FailsOnceOffPoolProducer(InternalBus bus, Publication publication)
    {
        _bus = bus;
        Publication = publication;
        _deliveryThread = new Thread(Deliver) { IsBackground = true, Name = "Off-pool delivery" };
        _deliveryThread.Start();
    }

    public Publication Publication { get; }
    public Activity? Span { get; set; }
    public IAmAMessageScheduler? Scheduler { get; set; }

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
            if (Interlocked.Increment(ref _sends) == 1)
            {
                delivered.SetException(new InvalidOperationException("The broker refused the first send"));
                continue;
            }

            _bus.Enqueue(message);
            delivered.SetResult();
        }
    }
}
