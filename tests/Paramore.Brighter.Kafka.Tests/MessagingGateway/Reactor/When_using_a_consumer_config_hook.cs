using System;
using Paramore.Brighter.Kafka.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.Kafka;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway.Reactor;

public class ConsumerConfigHookTests 
{
    private bool _callbackCalled = false;
    
    [Test]
    public async Task When_using_a_consumer_config_hook()
    {
        //arrange
        var subscription = new KafkaSubscription<MyCommand>(
            channelName: new ChannelName("TestChannel"), 
            routingKey: new RoutingKey("TestTopic_" + Guid.NewGuid()),
            groupId: "TestGroup_" + Guid.NewGuid(),
            numOfPartitions: 1,
            replicationFactor: 1,
            makeChannels: OnMissingChannel.Create,
            configHook: config =>
            {
                config.EnableMetricsPush = false; // Disable metrics push for testing
                _callbackCalled = true; // Set a flag to indicate the hook was called
            }
        );
       
        //act
        var consumer = new KafkaMessageConsumerFactory(
                new KafkaMessagingGatewayConfiguration
                {
                    Name = "Kafka Consumer Test",
                    BootStrapServers = ["localhost:9092"]
                })
            .Create(subscription
            );
        
        //assert
        await Assert.That(consumer).IsNotNull();
        await Assert.That(_callbackCalled).IsTrue();
    }
}
