using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Confluent.Kafka;
using Paramore.Brighter.Kafka.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;
using Xunit.Abstractions;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway.Proactor;

[Trait("Category", "Kafka")]
[Trait("Fragile", "CI")]
[Collection("Kafka")] //Kafka doesn't like multiple consumers of a partition
public class KafkaMessageConsumerUpdateOffsetAsync : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _queueName = Guid.NewGuid().ToString();
    private readonly string _topic = Guid.NewGuid().ToString();
    private readonly IAmAProducerRegistry _producerRegistry;
    private readonly string _partitionKey = Guid.NewGuid().ToString();

    public KafkaMessageConsumerUpdateOffsetAsync(ITestOutputHelper output)
    {
        _output = output;
        _producerRegistry = new KafkaProducerRegistryFactory(
            new KafkaMessagingGatewayConfiguration { Name = "Kafka Producer Send Test", BootStrapServers = new[] { "localhost:9092" } },
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

    //[Fact(Skip = "As it has to wait for the messages to flush, only tends to run well in debug")]
    [Fact]
    public async Task When_a_message_is_acknowledged_update_offset()
    {
        //Let topic propagate in the broker
        await Task.Delay(500);

        var groupId = Guid.NewGuid().ToString();
        var sentMessages = new ConcurrentDictionary<string, bool>();

        var routingKey = new RoutingKey(_topic);
        var producerAsync = ((IAmAMessageProducerAsync)_producerRegistry.LookupBy(routingKey));
        var producerConfirm = producerAsync as ISupportPublishConfirmation;
        producerConfirm.OnMessagePublished += delegate(bool success, string id)
        {
            if (success) sentMessages.TryUpdate(id, true, false);
        };

        //send x messages to Kafka
        var sentMessageIds = new string[10];
        for (int i = 0; i < 10; i++)
        {
            var msgId = Guid.NewGuid().ToString();
            sentMessages.TryAdd(msgId, false);
            sentMessageIds[i] = msgId;
            await producerAsync.SendAsync(
                new Message(
                    new MessageHeader(msgId, routingKey, MessageType.MT_COMMAND) { PartitionKey = _partitionKey },
                    new MessageBody($"test content [{_queueName}]")
                )
            );
        }

        //We should not need to flush, as the async does not queue work  - but in case this changes
        ((KafkaMessageProducer)producerAsync).Flush();

        //check we sent everything
        Assert.DoesNotContain(sentMessages, dr => dr.Value == false);

        //This will create, then dispose the consumer (flushing offsets)
        Message[] messages = await ConsumeMessagesAsync(groupId, batchLimit: 5);

        //check we read the first 5 messages
        Assert.Equal(5, messages.Length);
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(sentMessageIds[i], messages[i].Id);
        }

        //allow time for committed offsets to propagate in the broker
        await Task.Delay(TimeSpan.FromMilliseconds(5000));

        //This will create a new consumer for the same group
        Message[] newMessages = await ConsumeMessagesAsync(groupId, batchLimit: 5);

        //check we read the next 5 messages
        Assert.Equal(5, newMessages.Length);
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(sentMessageIds[i + 5], newMessages[i].Id);
        }
    }

    private async Task<Message[]> ConsumeMessagesAsync(string groupId, int batchLimit)
    {
        var consumedMessages = new List<Message>();
        await using (IAmAMessageConsumerAsync consumer = CreateConsumer(groupId))
        {
            for (int i = 0; i < batchLimit; i++)
            {
                consumedMessages.Add(await ConsumeMessageAsync(consumer));
            }

            //yield to allow the background CommitOffsets task to complete;
            //ReceiveAsync uses Task.Run for each receive, increasing thread pool pressure,
            //so the background commit may be delayed in the thread pool queue
            await Task.Delay(TimeSpan.FromMilliseconds(5000));
        }

        return consumedMessages.ToArray();

        async Task<Message> ConsumeMessageAsync(IAmAMessageConsumerAsync consumer)
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
                    {
                        await consumer.AcknowledgeAsync(messages[0]);
                        return messages[0];
                    }

                    //wait before retry
                    await Task.Delay(1000);
                }
                catch (ChannelFailureException cfx)
                {
                    //Lots of reasons to be here as Kafka propagates a topic, or the test cluster is still initializing
                    _output.WriteLine($" Failed to read from topic:{_topic} because {cfx.Message} attempt: {maxTries}");
                    await Task.Delay(1000);
                }
            } while (maxTries <= 10);

            return messages[0];
        }
    }

    private IAmAMessageConsumerAsync CreateConsumer(string groupId)
    {
        return new KafkaMessageConsumerFactory(
                new KafkaMessagingGatewayConfiguration { Name = "Kafka Consumer Test", BootStrapServers = new[] { "localhost:9092" } })
            .CreateAsync(new KafkaSubscription<MyCommand>
            (
                subscriptionName: new SubscriptionName("Paramore.Brighter.Tests"),
                channelName: new ChannelName(_queueName),
                routingKey: new RoutingKey(_topic),
                groupId: groupId,
                offsetDefault: AutoOffsetReset.Earliest,
                commitBatchSize: 5,
                messagePumpType: MessagePumpType.Proactor,
                numOfPartitions: 1, replicationFactor: 1, makeChannels: OnMissingChannel.Create));
    }

    public void Dispose()
    {
        _producerRegistry.Dispose();
    }
}
