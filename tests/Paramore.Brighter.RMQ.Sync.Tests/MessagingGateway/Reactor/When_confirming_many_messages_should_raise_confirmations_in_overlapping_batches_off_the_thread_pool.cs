using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.RMQ.Sync;
using Xunit;

namespace Paramore.Brighter.RMQ.Sync.Tests.MessagingGateway.Reactor;

[Trait("Category", "RMQ")]
[Collection("RMQ")]
public class RmqConfirmationBatchingTests : IDisposable
{
    private const int MessageCount = 50;
    private const int MaxBatchSize = 32;
    private readonly RoutingKey _routingKey = new(Guid.NewGuid().ToString());
    private readonly RmqMessageProducer _messageProducer;

    public RmqConfirmationBatchingTests()
    {
        var rmqConnection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange")
        };

        _messageProducer = new RmqMessageProducer(rmqConnection);

        //we need a queue to avoid a discard
        new QueueFactory(rmqConnection, new ChannelName(Guid.NewGuid().ToString()), new RoutingKeys(_routingKey))
            .Create(TimeSpan.FromMilliseconds(1000));
    }

    [Fact]
    public void When_confirming_many_messages_should_raise_confirmations_in_overlapping_batches_off_the_thread_pool()
    {
        //Arrange
        var inFlight = 0;
        var mostAtOnce = 0;
        var ranOnPool = false;
        using var allConfirmed = new CountdownEvent(MessageCount);
        ((ISupportPublishConfirmationAsync)_messageProducer).OnMessagePublishedAsync += async result =>
        {
            var now = Interlocked.Increment(ref inFlight);
            InterlockedMax(ref mostAtOnce, now);
            if (Thread.CurrentThread.IsThreadPoolThread) ranOnPool = true;
            await Task.Delay(10); // a handler that takes a little time, like marking an outbox row dispatched
            if (Thread.CurrentThread.IsThreadPoolThread) ranOnPool = true;
            Interlocked.Decrement(ref inFlight);
            allConfirmed.Signal();
        };

        var sent = Enumerable.Range(0, MessageCount)
            .Select(_ => new Message(
                new MessageHeader(Id.Random(), _routingKey, MessageType.MT_COMMAND),
                new MessageBody("confirm me")))
            .ToArray();

        //Act
        foreach (var message in sent)
            _messageProducer.Send(message);

        var completed = allConfirmed.Wait(TimeSpan.FromSeconds(30));

        //Assert
        Assert.True(completed, "Not every message was confirmed");
        Assert.InRange(mostAtOnce, 2, MaxBatchSize); // confirmations overlap, but never more than one batch
        Assert.False(ranOnPool, "A confirmation handler ran on a thread-pool thread");
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while ((current = Volatile.Read(ref target)) < value
               && Interlocked.CompareExchange(ref target, value, current) != current) { }
    }

    public void Dispose() => _messageProducer.Dispose();
}
