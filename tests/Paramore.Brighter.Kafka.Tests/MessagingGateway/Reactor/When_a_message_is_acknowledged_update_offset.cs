using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Confluent.Kafka;
using Paramore.Brighter.Kafka.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;
using Xunit.Abstractions;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway.Reactor;

[Trait("Category", "Kafka")]
[Collection("Kafka")] //Kafka doesn't like multiple consumers of a partition
public class KafkaMessageConsumerUpdateOffset : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _queueName = Guid.NewGuid().ToString();
    private readonly string _topic = Guid.NewGuid().ToString();
    private readonly IAmAProducerRegistry _producerRegistry;
    private readonly string _partitionKey = Guid.NewGuid().ToString();

    public KafkaMessageConsumerUpdateOffset(ITestOutputHelper output)
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
                    //These timeouts support running on a container using the same host as the tests, 
                    //your production values ought to be lower
                    MessageTimeoutMs = 2000,
                    RequestTimeoutMs = 2000,
                    MakeChannels = OnMissingChannel.Create
                }
            ]).Create();
    }

    //[Fact(Skip = "Fragile as commit thread needs to be scheduled to run")]
    [Fact]
    public async Task When_a_message_is_acknowldgede_update_offset()
    {
        // let topic propogate in the broker
        await Task.Delay(500);

        var groupId = Guid.NewGuid().ToString();

        var routingKey = new RoutingKey(_topic);
        var producer = ((IAmAMessageProducerSync)_producerRegistry.LookupBy(routingKey));

        //send x messages to Kafka
        var sentMessages = new string[10];
        for (int i = 0; i < 10; i++)
        {
            var msgId = Guid.NewGuid().ToString();
            producer.Send(
                new Message(
                    new MessageHeader(msgId, routingKey, MessageType.MT_COMMAND) { PartitionKey = _partitionKey },
                    new MessageBody($"test content [{_queueName}]")
                ));
            sentMessages[i] = msgId;
        }

        //ensure the messages are actually sent
        ((KafkaMessageProducer)producer).Flush();

        //This will create, then delete the consumer
        Message[] messages = await ConsumeMessages(groupId: groupId, batchLimit: 5);

        //check we read the first 5 messages
        Assert.Equal(5, messages.Length);
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(sentMessages[i], messages[i].Id);
        }

        //This will create a new consumer for the same group
        Message[] newMessages = await ConsumeMessages(groupId, batchLimit: 5);

        //check we read the next 5 messages
        Assert.Equal(5, newMessages.Length);
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(sentMessages[i + 5], newMessages[i].Id);
        }
    }

    private async Task<Message[]> ConsumeMessages(string groupId, int batchLimit)
    {
        var consumedMessages = new List<Message>();
        using (IAmAMessageConsumerSync consumer = CreateConsumer(groupId))
        {
            for (int i = 0; i < batchLimit; i++)
            {
                consumedMessages.Add(ConsumeMessage(consumer));
            }

            //yield to allow commits to flush
            await Task.Delay(TimeSpan.FromMilliseconds(5000));
        }

        return consumedMessages.ToArray();

        Message ConsumeMessage(IAmAMessageConsumerSync consumer)
        {
            Message[] messages = [new Message()];
            int maxTries = 0;
            do
            {
                try
                {
                    maxTries++;
                    //makes a blocking call to Kafka
                    messages = consumer.Receive(TimeSpan.FromMilliseconds(1000));

                    if (messages[0].Header.MessageType != MessageType.MT_NONE)
                    {
                        consumer.Acknowledge(messages[0]);
                        return messages[0];
                    }
                }
                catch (ChannelFailureException cfx)
                {
                    //Lots of reasons to be here as Kafka propagates a topic, or the test cluster is still initializing
                    _output.WriteLine($" Failed to read from topic:{_topic} because {cfx.Message} attempt: {maxTries}");
                    Task.Delay(1000).GetAwaiter().GetResult();
                }
            } while (maxTries <= 10);

            return messages[0];
        }
    }

    private IAmAMessageConsumerSync CreateConsumer(string groupId)
    {
        return new KafkaMessageConsumerFactory(
                new KafkaMessagingGatewayConfiguration
                {
                    Name = "Kafka Consumer Test", BootStrapServers = new[] { "localhost:9092" }
                })
            .Create(new KafkaSubscription<MyCommand>
            (
                subscriptionName: new SubscriptionName("Paramore.Brighter.Tests"),
                channelName: new ChannelName(_queueName),
                routingKey: new RoutingKey(_topic),
                groupId: groupId,
                offsetDefault: AutoOffsetReset.Earliest,
                commitBatchSize: 5,
                numOfPartitions: 1,
                replicationFactor: 1,
                messagePumpType: MessagePumpType.Reactor,
                makeChannels: OnMissingChannel.Create
            ));
    }

    public void Dispose()
    {
        _producerRegistry?.Dispose();
    }
}
