using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.AWSSQS.V4;

namespace Paramore.Brighter.AWS.V4.Tests.MessagingGateway;

// Supplies the FIFO metadata the transport-agnostic canonical messages omit: a MessageGroupId
// (FIFO requires one) and a unique MessageDeduplicationId (a fresh id per send) so that two
// identical-content messages are not collapsed by content-based deduplication — without which the
// second of two look-alike messages is silently dropped and "receive the next message" asserts see
// MT_NONE. Delegates everything else to the wrapped producer. Shared by the SQS FIFO and SNS FIFO
// conformance providers (the wrapped producer is SqsMessageProducer or SnsMessageProducer
// respectively; both implement the sync and async producer interfaces).
internal sealed class FifoMetadataProducer : IAmAMessageProducerSync, IAmAMessageProducerAsync
{
    private const string ConformanceMessageGroup = "conformance";

    private readonly IAmAMessageProducerSync _inner;
    private readonly IAmAMessageProducerAsync _innerAsync;

    public FifoMetadataProducer(IAmAMessageProducerSync inner)
    {
        _inner = inner;
        _innerAsync = (IAmAMessageProducerAsync)inner;
    }

    public Publication Publication => _inner.Publication;

    public Activity? Span
    {
        get => _inner.Span;
        set => _inner.Span = value;
    }

    public IAmAMessageScheduler? Scheduler
    {
        get => _inner.Scheduler;
        set => _inner.Scheduler = value;
    }

    public void Send(Message message)
    {
        StampFifoMetadata(message);
        _inner.Send(message);
    }

    public void SendWithDelay(Message message, TimeSpan? delay)
    {
        StampFifoMetadata(message);
        _inner.SendWithDelay(message, delay);
    }

    public Task SendAsync(Message message, CancellationToken cancellationToken = default)
    {
        StampFifoMetadata(message);
        return _innerAsync.SendAsync(message, cancellationToken);
    }

    public Task SendWithDelayAsync(Message message, TimeSpan? delay, CancellationToken cancellationToken = default)
    {
        StampFifoMetadata(message);
        return _innerAsync.SendWithDelayAsync(message, delay, cancellationToken);
    }

    public void Dispose() => _inner.Dispose();

    public ValueTask DisposeAsync() => _innerAsync.DisposeAsync();

    private static void StampFifoMetadata(Message message)
    {
        if (PartitionKey.IsNullOrEmpty(message.Header.PartitionKey))
        {
            message.Header.PartitionKey = new PartitionKey(ConformanceMessageGroup);
        }

        // A fresh id per send, not message.Id: the canonical suite reuses one message builder, so its
        // two "distinct" messages share an id and body. FIFO would treat the second as a duplicate and
        // drop it; a unique dedup id per send makes every send a distinct FIFO message.
        message.Header.Bag[HeaderNames.DeduplicationId] = Uuid.NewAsString();
    }
}
