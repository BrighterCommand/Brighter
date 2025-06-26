using System;
using Paramore.Brighter.Kafka.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway;

public class ConsumerConfigHookTests : IDisposable
{
    private bool _callbackCalled = false;
    
    [Fact]
    public void When_using_a_consumer_config_hook()
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
        Assert.NotNull(consumer);
        Assert.True(_callbackCalled, "The consumer config hook should have been called.");
    }

    public void Dispose()
    {
    }
}
