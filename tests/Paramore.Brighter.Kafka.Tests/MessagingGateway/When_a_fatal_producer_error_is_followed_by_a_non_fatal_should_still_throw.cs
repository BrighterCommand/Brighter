using System;
using Confluent.Kafka;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway;

[Trait("Category", "Kafka")]
[Collection("Kafka")]
public class When_a_fatal_producer_error_is_followed_by_a_non_fatal_should_still_throw : IDisposable
{
    private readonly KafkaMessageProducer _producer;

    public When_a_fatal_producer_error_is_followed_by_a_non_fatal_should_still_throw()
    {
        _producer = new KafkaMessageProducer(
            new KafkaMessagingGatewayConfiguration
            {
                Name = "test", BootStrapServers = ["localhost:9092"]
            },
            new KafkaPublication
            {
                Topic = new RoutingKey("test.topic"),
                MakeChannels = OnMissingChannel.Assume,
                // Keep flush-on-dispose fast: the pre-fix (red) path enqueues a message that can never
                // be delivered without a broker, and Dispose flushes it. A short timeout bounds that wait.
                MessageTimeoutMs = 500
            });
        _producer.Init();
    }

    [Fact]
    public void When_the_non_fatal_error_arrives_after_the_fatal_error_send_still_throws()
    {
        //Arrange - librdkafka reports a fatal error, then immediately a non-fatal one. Errors arrive
        //in bursts, and the fatal state is meant to be a one-way "producer is dead" latch.
        _producer.HandleError(new Error(ErrorCode.Local_Fatal, "fatal error", isFatal: true));
        _producer.HandleError(new Error(ErrorCode.Local_TimedOut, "idle socket timed out", isFatal: false));

        var message = new Message(
            new MessageHeader("test-id", new RoutingKey("test.topic"), MessageType.MT_COMMAND),
            new MessageBody("test body"));

        //Act
        var exception = Record.Exception(() => _producer.Send(message));

        //Assert - the fatal condition must remain latched, so Send reports the producer as unrecoverable
        Assert.IsType<ChannelFailureException>(exception);
    }

    public void Dispose()
    {
        _producer?.Dispose();
    }
}
