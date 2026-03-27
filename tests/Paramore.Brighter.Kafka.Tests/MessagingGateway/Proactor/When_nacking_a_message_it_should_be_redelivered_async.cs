using System;
using System.Threading.Tasks;
using Confluent.Kafka;
using Paramore.Brighter.Kafka.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;
using Xunit.Abstractions;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway.Proactor;

[Trait("Category", "Kafka")]
[Collection("Kafka")] //Kafka doesn't like multiple consumers of a partition
public class KafkaMessageConsumerNackRedeliveryAsync : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _queueName = Guid.NewGuid().ToString();
    private readonly string _topic = Guid.NewGuid().ToString();
    private readonly IAmAProducerRegistry _producerRegistry;
    private readonly string _partitionKey = Guid.NewGuid().ToString();

    public KafkaMessageConsumerNackRedeliveryAsync(ITestOutputHelper output)
    {
        _output = output;
        _producerRegistry = new KafkaProducerRegistryFactory(
            new KafkaMessagingGatewayConfiguration
            {
                Name = "Kafka Producer Send Test", BootStrapServers = new[] { "localhost:9092" }
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
    public async Task When_nacking_a_message_async_it_should_be_redelivered()
    {
        // let topic propagate in the broker
        await Task.Delay(500);

        var groupId = Guid.NewGuid().ToString();

        //Arrange - send a message to Kafka
        var routingKey = new RoutingKey(_topic);
        var producer = (IAmAMessageProducerAsync)_producerRegistry.LookupBy(routingKey);
        var sentMessageId = Guid.NewGuid().ToString();
        var sentBody = $"test content [{_queueName}]";

        await producer.SendAsync(
            new Message(
                new MessageHeader(sentMessageId, routingKey, MessageType.MT_COMMAND) { PartitionKey = _partitionKey },
                new MessageBody(sentBody)
            ));
        ((KafkaMessageProducer)producer).Flush();

        await using IAmAMessageConsumerAsync consumer = CreateConsumer(groupId);

        //Act - receive the message, nack it, then receive again
        var firstReceive = await ReceiveMessageAsync(consumer);
        Assert.Equal(sentMessageId, firstReceive.Id);

        await consumer.NackAsync(firstReceive);

        var secondReceive = await ReceiveMessageAsync(consumer);

        //Assert - the same message should be redelivered
        Assert.Equal(sentMessageId, secondReceive.Id);
        Assert.Equal(sentBody, secondReceive.Body.Value);
    }

    [Fact]
    public async Task When_acking_later_message_async_it_should_not_skip_nacked_message()
    {
        // let topic propagate in the broker
        await Task.Delay(500);

        var groupId = Guid.NewGuid().ToString();

        //Arrange - send two messages to Kafka
        var routingKey = new RoutingKey(_topic);
        var producer = (IAmAMessageProducerAsync)_producerRegistry.LookupBy(routingKey);
        var firstMessageId = Guid.NewGuid().ToString();
        var secondMessageId = Guid.NewGuid().ToString();

        await producer.SendAsync(
            new Message(
                new MessageHeader(firstMessageId, routingKey, MessageType.MT_COMMAND) { PartitionKey = _partitionKey },
                new MessageBody($"first message [{_queueName}]")
            ));
        await producer.SendAsync(
            new Message(
                new MessageHeader(secondMessageId, routingKey, MessageType.MT_COMMAND) { PartitionKey = _partitionKey },
                new MessageBody($"second message [{_queueName}]")
            ));
        ((KafkaMessageProducer)producer).Flush();

        await using IAmAMessageConsumerAsync consumer = CreateConsumer(groupId);

        //Act - receive message 1, nack it; receive message 1 again (redelivered), then ack it
        var firstReceive = await ReceiveMessageAsync(consumer);
        Assert.Equal(firstMessageId, firstReceive.Id);

        await consumer.NackAsync(firstReceive);

        // After nack, consumer should redeliver the first message, not skip to the second
        var redelivered = await ReceiveMessageAsync(consumer);

        //Assert - the nacked message is redelivered, not skipped by the second message's existence
        Assert.Equal(firstMessageId, redelivered.Id);

        // Now ack the redelivered message and confirm we get the second message
        await consumer.AcknowledgeAsync(redelivered);

        var secondReceive = await ReceiveMessageAsync(consumer);
        Assert.NotEqual(MessageType.MT_NONE, secondReceive.Header.MessageType);
        Assert.Equal(secondMessageId, secondReceive.Id);
    }

    private async Task<Message> ReceiveMessageAsync(IAmAMessageConsumerAsync consumer)
    {
        Message[] messages = [new Message()];
        int maxTries = 0;
        do
        {
            try
            {
                maxTries++;
                messages = await consumer.ReceiveAsync(TimeSpan.FromMilliseconds(1000));

                if (messages[0].Header.MessageType != MessageType.MT_NONE)
                    return messages[0];

                await Task.Delay(1000);
            }
            catch (ChannelFailureException cfx)
            {
                _output.WriteLine($" Failed to read from topic:{_topic} because {cfx.Message} attempt: {maxTries}");
                await Task.Delay(1000);
            }
        } while (maxTries <= 10);

        return messages[0];
    }

    private IAmAMessageConsumerAsync CreateConsumer(string groupId)
    {
        return new KafkaMessageConsumerFactory(
                new KafkaMessagingGatewayConfiguration
                {
                    Name = "Kafka Consumer Test", BootStrapServers = new[] { "localhost:9092" }
                })
            .CreateAsync(new KafkaSubscription<MyCommand>
            (
                subscriptionName: new SubscriptionName("Paramore.Brighter.Tests"),
                channelName: new ChannelName(_queueName),
                routingKey: new RoutingKey(_topic),
                groupId: groupId,
                offsetDefault: AutoOffsetReset.Earliest,
                commitBatchSize: 5,
                numOfPartitions: 1,
                replicationFactor: 1,
                messagePumpType: MessagePumpType.Proactor,
                makeChannels: OnMissingChannel.Create
            ));
    }

    public void Dispose()
    {
        _producerRegistry?.Dispose();
    }
}
