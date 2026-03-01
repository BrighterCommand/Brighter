using System;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;
using Xunit.Abstractions;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway.Reactor;

[Trait("Category", "Kafka")]
[Collection("Kafka")]   //Kafka doesn't like multiple consumers of a partition
public class KafkaProducerAssumeTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _queueName = Guid.NewGuid().ToString(); 
    private readonly string _topic = Guid.NewGuid().ToString();
    private readonly IAmAProducerRegistry _producerRegistry;
    private readonly string _partitionKey = Guid.NewGuid().ToString();

    public KafkaProducerAssumeTests(ITestOutputHelper output)
    {
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
            
    }

    //Watch your local Docker container when checking failures for this test, should be 
    //KAFKA_AUTO_CREATE_TOPICS_ENABLE: "false"
    [Fact]
    [Trait("Fragile", "CI")]
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
            
        bool messagePublished = false;
        var producer = _producerRegistry.LookupBy(routingKey);
        var producerConfirm = producer as ISupportPublishConfirmation;
        producerConfirm.OnMessagePublished += delegate(bool success, string id)
        {
            if (success) messagePublished = true;
        };
            
        ((IAmAMessageProducerSync)producer).Send(message);

        ((KafkaMessageProducer)producer).Flush();

        Assert.False(messagePublished);
    }

    public void Dispose()
    {
        _producerRegistry?.Dispose();
    }
}
