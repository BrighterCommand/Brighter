using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.AWSSQS;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway;

// Supplies the FIFO metadata the transport-agnostic canonical messages omit: a MessageGroupId (FIFO
// requires one) and a MessageDeduplicationId, which is mandatory here because every conformance FIFO
// queue and topic is created with content-based deduplication switched off, and a FIFO send carrying
// neither is rejected outright. Delegates everything else to the wrapped producer. Shared by the SQS
// FIFO and SNS FIFO conformance providers (the wrapped producer is SqsMessageProducer or
// SnsMessageProducer respectively; both implement the sync and async producer interfaces).
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

        // Mandatory, not defensive: the conformance FIFO queues and topics are all created with
        // contentBasedDeduplication: false, and the gateway sets MessageDeduplicationId only when the
        // bag carries one (SqsMessageSender.SetFifoQueueProperties, SnsMessagePublisher
        // .ConfigureFifoSettings), so without this stamp every FIFO send is rejected. A fresh id per
        // send keeps each send a distinct FIFO message whatever the message itself carries.
        message.Header.Bag[HeaderNames.DeduplicationId] = Uuid.NewAsString();
    }
}
