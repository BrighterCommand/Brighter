using System;
using System.Threading.Tasks;
using Paramore.Brighter.Kafka.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;
using Xunit.Abstractions;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway.Proactor;

[Trait("Category", "Kafka")]
[Trait("Fragile", "CI")]
[Collection("Kafka")]   //Kafka doesn't like multiple consumers of a partition
public class KafkaConsumerDeclareTestsAsync : IAsyncDisposable, IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _queueName = Guid.NewGuid().ToString();
    private readonly string _topic = Guid.NewGuid().ToString();
    private readonly IAmAProducerRegistry _producerRegistry;
    private readonly IAmAMessageConsumerAsync _consumer;
    private readonly string _partitionKey = Guid.NewGuid().ToString();

    public KafkaConsumerDeclareTestsAsync(ITestOutputHelper output)
    {
        const string groupId = "Kafka Message Producer Send Test";
        _output = output;
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

    //[Fact(Skip = "As it has to wait for the messages to flush, only tends to run well in debug")]
    [Fact]
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
                _output.WriteLine($" Failed to read from topic:{_topic} because {cfx.Message} attempt: {maxTries}");
            }

        } while (maxTries <= 3);

        Assert.Single(messages);
        Assert.Equal(MessageType.MT_COMMAND, messages[0].Header.MessageType);
        Assert.Equal(_partitionKey, messages[0].Header.PartitionKey);
        Assert.Equal(message.Body.Value, messages[0].Body.Value);
    }

    public void Dispose()
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
