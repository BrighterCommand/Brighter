using System;
using System.Threading.Tasks;
using Paramore.Brighter.Kafka.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.Kafka;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway.Proactor;

[Category("Kafka")]
public class KafkaConsumerDeclareTestsAsync : IAsyncDisposable
{
    private readonly string _queueName = Guid.NewGuid().ToString();
    private readonly string _topic = Guid.NewGuid().ToString();
    private readonly IAmAProducerRegistry _producerRegistry;
    private readonly IAmAMessageConsumerAsync _consumer;
    private readonly string _partitionKey = Guid.NewGuid().ToString();

    public KafkaConsumerDeclareTestsAsync()
    {
        string groupId = Guid.NewGuid().ToString();
        _producerRegistry = new KafkaProducerRegistryFactory(
            new KafkaMessagingGatewayConfiguration
            {
                Name = "Kafka Producer Send Test",
                BootStrapServers = new[] {"localhost:9092"}
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
                    MakeChannels = OnMissingChannel.Assume
                }
            ]).Create();

        _consumer = new KafkaMessageConsumerFactory(
                new KafkaMessagingGatewayConfiguration
                {
                    Name = "Kafka Consumer Test",
                    BootStrapServers = new[] { "localhost:9092" }
                })
            .CreateAsync(new KafkaSubscription<MyCommand>(
                channelName: new ChannelName(_queueName),
                routingKey: new RoutingKey(_topic),
                groupId: groupId,
                messagePumpType: MessagePumpType.Proactor,
                numOfPartitions: 1, replicationFactor: 1, makeChannels: OnMissingChannel.Create));

    }

    //[Test, Skip("As it has to wait for the messages to flush, only tends to run well in debug")]
    [Test]
    public async Task When_a_consumer_declares_topics()
    {
        //Let topic propagate in the broker
        await Task.Delay(1000); 
        
        var routingKey = new RoutingKey(_topic);

        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND)
            {
                PartitionKey = _partitionKey
            },
            new MessageBody($"test content [{_queueName}]")
        );

        var producerAsync = ((IAmAMessageProducerAsync)_producerRegistry.LookupBy(routingKey));
        await producerAsync.SendAsync(message);
        
        //We should not need to flush, as the async does not queue work  - but in case this changes
        ((KafkaMessageProducer)producerAsync).Flush();

        //allow broker time to propogate
        await Task.Delay(3000);

        Message[] messages = [];
        int maxTries = 0;
        do
        {
            try
            {
                maxTries++;
                //use TimeSpan.Zero to avoid blocking
                messages = await _consumer.ReceiveAsync(TimeSpan.FromMilliseconds(1000));
                await _consumer.AcknowledgeAsync(messages[0]);

                if (messages[0].Header.MessageType != MessageType.MT_NONE)
                    break;
                
                //wait before retry
                await Task.Delay(1000);

            }
            catch (ChannelFailureException cfx)
            {
                //Lots of reasons to be here as Kafka propagates a topic, or the test cluster is still initializing
                Console.WriteLine($" Failed to read from topic:{_topic} because {cfx.Message} attempt: {maxTries}");
                await Task.Delay(1000);
            }

        } while (maxTries <= 10);

        await Assert.That(messages).HasSingleItem();
        await Assert.That(messages[0].Header.MessageType).IsEqualTo(MessageType.MT_COMMAND);
        await Assert.That(messages[0].Header.PartitionKey).IsEqualTo(_partitionKey);
        await Assert.That(messages[0].Body.Value).IsEqualTo(message.Body.Value);
    }

    [After(Test)]
    public async Task Cleanup()
    {
        _producerRegistry?.Dispose();
        ((IAmAMessageConsumerSync)_consumer).Dispose();
    }

    public async ValueTask DisposeAsync()
    {
            _producerRegistry.Dispose();
            await _consumer.DisposeAsync();
            
    }
}

