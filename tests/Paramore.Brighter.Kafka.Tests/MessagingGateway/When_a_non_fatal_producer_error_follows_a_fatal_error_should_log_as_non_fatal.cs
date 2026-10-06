using System;
using System.Linq;
using Confluent.Kafka;
using Paramore.Brighter.MessagingGateway.Kafka;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using Xunit;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway;

[Trait("Category", "Kafka")]
[Collection("Kafka")]
public class When_a_non_fatal_producer_error_follows_a_fatal_error_should_log_as_non_fatal : IDisposable
{
    private readonly KafkaMessageProducer _producer;

    public When_a_non_fatal_producer_error_follows_a_fatal_error_should_log_as_non_fatal()
    {
        _producer = new KafkaMessageProducer(
            new KafkaMessagingGatewayConfiguration
            {
                Name = "test", BootStrapServers = ["localhost:9092"]
            },
            new KafkaPublication
            {
                Topic = new RoutingKey("test.topic"),
                MakeChannels = OnMissingChannel.Assume
            });
        // No Init()/Send() required: HandleError only sets the latch and logs, so the producer never
        // needs to contact a broker for this test.
    }

    [Fact]
    public void When_the_non_fatal_error_arrives_after_the_fatal_error_it_is_logged_as_non_fatal()
    {
        using var context = TestCorrelator.CreateContext();

        //Arrange / Act - a fatal error latches the producer as dead; a following non-fatal error must
        //still be logged at its own (non-fatal) level, not escalated to fatal just because the
        //producer is now latched. Latching the state must not couple the logging decision to it.
        _producer.HandleError(new Error(ErrorCode.Local_Fatal, "a fatal producer error", isFatal: true));
        _producer.HandleError(new Error(ErrorCode.Local_TimedOut, "an idle socket non fatal timeout", isFatal: false));

        //Assert - the non-fatal error was logged at Warning (NonFatalProducerError), not Error (FatalProducerError)
        var nonFatalEvent = TestCorrelator.GetLogEventsFromCurrentContext()
            .Single(e => e.RenderMessage().Contains("an idle socket non fatal timeout"));
        Assert.Equal(LogEventLevel.Warning, nonFatalEvent.Level);
    }

    public void Dispose()
    {
        _producer?.Dispose();
    }
}
