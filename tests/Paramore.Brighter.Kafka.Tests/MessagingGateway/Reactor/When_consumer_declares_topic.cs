using System;
using System.Threading.Tasks;
using Paramore.Brighter.Kafka.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;
using Xunit.Abstractions;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway.Reactor;

[Trait("Category", "Kafka")]
[Collection("Kafka")]   //Kafka doesn't like multiple consumers of a partition
public class KafkaConsumerDeclareTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _queueName = Guid.NewGuid().ToString(); 
    private readonly string _topic = Guid.NewGuid().ToString();
    private readonly IAmAProducerRegistry _producerRegistry;
    private readonly IAmAMessageConsumerSync _consumer;
    private readonly string _partitionKey = Guid.NewGuid().ToString();

    public KafkaConsumerDeclareTests (ITestOutputHelper output)
    {
        string groupId = Guid.NewGuid().ToString();
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
            .Create(new KafkaSubscription<MyCommand>(
                    channelName: new ChannelName(_queueName), 
                    routingKey: new RoutingKey(_topic),
                    groupId: groupId,
                    numOfPartitions: 1,
                    replicationFactor: 1,
                    messagePumpType: MessagePumpType.Reactor,
                    makeChannels: OnMissingChannel.Create
                )
            );
             
    }

    [Fact]
    public async Task When_a_consumer_declares_topics()
    {
        //Let topic propogate
        await Task.Delay(500);
        
        var routingKey = new RoutingKey(_topic);
            
        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND)
            {
                PartitionKey = _partitionKey
            },
            new MessageBody($"test content [{_queueName}]")
        );

        var producer = ((IAmAMessageProducerSync)_producerRegistry.LookupBy(routingKey));
        producer.Send(message);
        
        //ensure the messages are sent
        ((KafkaMessageProducer)producer).Flush();

        Message receivedMessage = ConsumeMessage();

        Assert.Equal(MessageType.MT_COMMAND, receivedMessage.Header.MessageType);
        Assert.Equal(_partitionKey, receivedMessage.Header.PartitionKey);
        Assert.Equal(message.Body.Value, receivedMessage.Body.Value);
    }

    private Message ConsumeMessage()
    {
        Message[] messages = new Message[0];
        int maxTries = 0;
        do
        {
            try
            {
                maxTries++;
                messages = _consumer.Receive(TimeSpan.FromMilliseconds(10000));
                _consumer.Acknowledge(messages[0]);
                    
                if (messages[0].Header.MessageType != MessageType.MT_NONE)
                    break;
                        
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

    public void Dispose()
    {
        _producerRegistry?.Dispose();
        _consumer?.Dispose();
    }
}
