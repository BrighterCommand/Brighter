using System;
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
[Collection("Kafka")]   //Kafka doesn't like multiple consumers of a partition
public class KafkaMessageConsumerPreservesOrderAsync : IDisposable
{
    private readonly string _queueName = Guid.NewGuid().ToString();
    private readonly string _topic = Guid.NewGuid().ToString();
    private readonly IAmAProducerRegistry _producerRegistry;
    private readonly string _partitionKey = Guid.NewGuid().ToString();
    private readonly string _kafkaGroupId = Guid.NewGuid().ToString();
    private readonly ITestOutputHelper _output;

    public KafkaMessageConsumerPreservesOrderAsync(ITestOutputHelper output)
    {
        _output = output;
        _producerRegistry = new KafkaProducerRegistryFactory(
            new KafkaMessagingGatewayConfiguration
            {
                Name = "Kafka Producer Send Test",
                BootStrapServers = new[] {"localhost:9092"}
            },
            new[] {new KafkaPublication
            {
                Topic = new RoutingKey(_topic),
                NumPartitions = 1,
                ReplicationFactor = 1,
                //These timeouts support running on a container using the same host as the tests,
                //your production values ought to be lower
                MessageTimeoutMs = 2000,
                RequestTimeoutMs = 2000,
                MakeChannels = OnMissingChannel.Create
            }}).Create();
    }

    //[Fact(Skip = "As it has to wait for the messages to flush, only tends to run well in debug")]
    [Fact]
    public async Task When_a_message_is_sent_keep_order()
    {
        //Let topic propagate in the broker
        await Task.Delay(500);

        IAmAMessageConsumerAsync consumer = null;

        var routingKey = new RoutingKey(_topic);

        var producerAsync = ((IAmAMessageProducerAsync)_producerRegistry.LookupBy(routingKey));
        try
        {
            //Send a sequence of messages to Kafka
            var msgId = await SendMessageAsync(producerAsync, routingKey);
            var msgId2 = await SendMessageAsync(producerAsync, routingKey);
            var msgId3 = await SendMessageAsync(producerAsync, routingKey);
            var msgId4 = await SendMessageAsync(producerAsync, routingKey);

            //We should not need to flush, as the async does not queue work  - but in case this changes
            ((KafkaMessageProducer)producerAsync).Flush();

            //allow messages time to propogate
            await Task.Delay(3000);

            consumer = CreateConsumer();

            //Now read those messages in order

            var firstMessage = await ConsumeMessagesAsync(consumer);
            var message = firstMessage.First();
            Assert.Equal(msgId, message.Id);
            await consumer.AcknowledgeAsync(message);

            var secondMessage = await ConsumeMessagesAsync(consumer);
            message = secondMessage.First();
            Assert.Equal(msgId2, message.Id);
            await consumer.AcknowledgeAsync(message);

            var thirdMessages = await ConsumeMessagesAsync(consumer);
            message = thirdMessages.First();
            Assert.Equal(msgId3, message.Id);
            await consumer.AcknowledgeAsync(message);

            var fourthMessage = await ConsumeMessagesAsync(consumer);
            message = fourthMessage.First();
            Assert.Equal(msgId4, message.Id);
            await consumer.AcknowledgeAsync(message);
        }
        finally
        {
            if (consumer != null)
            {
                await consumer.DisposeAsync();
            }
        }
    }

    private async Task<string> SendMessageAsync(IAmAMessageProducerAsync producerAsync, RoutingKey routingKey)
    {
        var messageId = Guid.NewGuid().ToString();

       await producerAsync.SendAsync(
            new Message(
                new MessageHeader(messageId, routingKey, MessageType.MT_COMMAND)
                {
                    PartitionKey = _partitionKey
                },
                new MessageBody($"test content [{_queueName}]")
            )
        );

        return messageId;
    }

    private async Task<IEnumerable<Message>> ConsumeMessagesAsync(IAmAMessageConsumerAsync consumer)
    {
        var messages = Array.Empty<Message>();
        int maxTries = 0;
        do
        {
            try
            {
                maxTries++;
                //use TimeSpan.Zero to avoid blocking
                messages = await consumer.ReceiveAsync(TimeSpan.FromMilliseconds(1000));

                if (messages[0].Header.MessageType != MessageType.MT_NONE)
                    break;

                //wait before retry
                await Task.Delay(1000);
            }
            catch (ChannelFailureException cfx)
            {
                //Lots of reasons to be here as Kafka propagates a topic, or the test cluster is still initializing
                _output.WriteLine($" Failed to read from topic:{_topic} because {cfx.Message} attempt: {maxTries}");
            }
        } while (maxTries <= 3);

        return messages;
    }

    private IAmAMessageConsumerAsync CreateConsumer()
    {
        return new KafkaMessageConsumerFactory(
                new KafkaMessagingGatewayConfiguration
                {
                    Name = "Kafka Consumer Test",
                    BootStrapServers = new[] { "localhost:9092" }
                })
            .CreateAsync(new KafkaSubscription<MyCommand>(
                subscriptionName: new SubscriptionName("Paramore.Brighter.Tests"),
                channelName: new ChannelName(_queueName),
                routingKey: new RoutingKey(_topic),
                groupId: _kafkaGroupId,
                offsetDefault: AutoOffsetReset.Earliest,
                commitBatchSize: 1,
                messagePumpType: MessagePumpType.Proactor,
                numOfPartitions: 1, replicationFactor: 1, makeChannels: OnMissingChannel.Create));
    }

    public void Dispose()
    {
        _producerRegistry.Dispose();
    }
}
