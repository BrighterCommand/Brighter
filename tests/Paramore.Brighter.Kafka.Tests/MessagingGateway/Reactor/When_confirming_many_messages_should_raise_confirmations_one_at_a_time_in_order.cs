using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway.Reactor;

[Trait("Category", "Kafka")]
[Collection("Kafka")]
public class KafkaConfirmationOrderingTests : IDisposable
{
    private const int MessageCount = 50;
    private readonly string _topic = Guid.NewGuid().ToString();
    private readonly IAmAProducerRegistry _producerRegistry;

    public KafkaConfirmationOrderingTests()
    {
        _producerRegistry = new KafkaProducerRegistryFactory(
            new KafkaMessagingGatewayConfiguration
            {
                Name = "Kafka Confirmation Ordering Test", BootStrapServers = new[] { "localhost:9092" }
            },
            [
                new KafkaPublication
                {
                    Topic = new RoutingKey(_topic),
                    NumPartitions = 1,
                    ReplicationFactor = 1,
                    MessageTimeoutMs = 2000,
                    RequestTimeoutMs = 2000,
                    MakeChannels = OnMissingChannel.Create
                }
            ]).Create();
    }

    [Fact]
    public async Task When_confirming_many_messages_should_raise_confirmations_one_at_a_time_in_order()
    {
        //Arrange
        await Task.Delay(500); //Let the topic propagate in the broker

        var routingKey = new RoutingKey(_topic);
        var producer = _producerRegistry.LookupBy(routingKey);
        var confirmations = (ISupportPublishConfirmationAsync)producer;

        var inFlight = 0;
        var mostAtOnce = 0;
        var confirmedInOrder = new ConcurrentQueue<Id>();
        using var allConfirmed = new CountdownEvent(MessageCount);
        confirmations.OnMessagePublishedAsync += async result =>
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
                new MessageHeader(Id.Random(), routingKey, MessageType.MT_EVENT),
                new MessageBody("confirm me")))
            .ToArray();

        //Act
        foreach (var message in sent)
            ((IAmAMessageProducerSync)producer).Send(message);

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

    public void Dispose() => _producerRegistry.Dispose();
}
