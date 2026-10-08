using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.RMQ.Sync;
using Xunit;

namespace Paramore.Brighter.RMQ.Sync.Tests.MessagingGateway.Reactor;

[Trait("Category", "RMQ")]
[Collection("RMQ")]
public class RmqConfirmationOrderingTests : IDisposable
{
    private const int MessageCount = 50;
    private readonly RoutingKey _routingKey = new(Guid.NewGuid().ToString());
    private readonly RmqMessageProducer _messageProducer;

    public RmqConfirmationOrderingTests()
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
    public void When_confirming_many_messages_should_raise_confirmations_one_at_a_time_in_order()
    {
        //Arrange
        var inFlight = 0;
        var mostAtOnce = 0;
        var confirmedInOrder = new ConcurrentQueue<Id>();
        using var allConfirmed = new CountdownEvent(MessageCount);
        ((ISupportPublishConfirmationAsync)_messageProducer).OnMessagePublishedAsync += async result =>
        {
            var now = Interlocked.Increment(ref inFlight);
            InterlockedMax(ref mostAtOnce, now);
            await Task.Delay(10); // a handler that takes a little time, like marking an outbox row dispatched
            confirmedInOrder.Enqueue(result.MessageId);
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
        Assert.Equal(1, mostAtOnce);
        Assert.Equal(sent.Select(m => m.Id), confirmedInOrder);
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while ((current = Volatile.Read(ref target)) < value
               && Interlocked.CompareExchange(ref target, value, current) != current) { }
    }

    public void Dispose() => _messageProducer.Dispose();
}
