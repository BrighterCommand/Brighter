using System;
using Confluent.Kafka;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway;

[Trait("Category", "Kafka")]
[Collection("Kafka")]
public class When_a_fatal_consumer_error_is_followed_by_a_non_fatal_should_still_throw : IDisposable
{
    private readonly KafkaMessageConsumer _consumer;

    public When_a_fatal_consumer_error_is_followed_by_a_non_fatal_should_still_throw()
    {
        _consumer = new KafkaMessageConsumer(
            new KafkaMessagingGatewayConfiguration
            {
                Name = "test", BootStrapServers = ["localhost:9092"]
            },
            routingKey: new RoutingKey("test.topic"),
            groupId: "test-group",
            offsetDefault: AutoOffsetReset.Earliest,
            numPartitions: 1,
            replicationFactor: 1,
            makeChannels: OnMissingChannel.Assume
        );
    }

    [Fact]
    public void When_the_non_fatal_error_arrives_after_the_fatal_error_receive_still_throws()
    {
        //Arrange - librdkafka reports a fatal error, then immediately a non-fatal one. Errors arrive
        //in bursts, and the fatal state is meant to be a one-way "consumer is dead" latch.
        _consumer.HandleError(new Error(ErrorCode.Local_Fatal, "fatal error", isFatal: true));
        _consumer.HandleError(new Error(ErrorCode.Local_TimedOut, "idle socket timed out", isFatal: false));

        //Act - the consume loop checks the latch before consuming; TimeSpan.Zero avoids any broker round-trip
        var exception = Record.Exception(() => _consumer.Receive(TimeSpan.Zero));

        //Assert - the fatal condition must remain latched, so Receive reports the channel as failed
        Assert.IsType<ChannelFailureException>(exception);
    }

    public void Dispose()
    {
        _consumer?.Dispose();
    }
}
