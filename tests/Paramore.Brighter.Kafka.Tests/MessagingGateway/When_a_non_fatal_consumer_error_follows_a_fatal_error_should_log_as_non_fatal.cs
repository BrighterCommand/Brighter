using System;
using System.Linq;
using Confluent.Kafka;
using Paramore.Brighter.MessagingGateway.Kafka;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using System.Threading.Tasks;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway;

[Property("Category", "Kafka")]
[System.Obsolete]
public class When_a_non_fatal_consumer_error_follows_a_fatal_error_should_log_as_non_fatal : IDisposable
{
    private readonly KafkaMessageConsumer _consumer;

    public When_a_non_fatal_consumer_error_follows_a_fatal_error_should_log_as_non_fatal()
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

    [Test]
    public async Task When_the_non_fatal_error_arrives_after_the_fatal_error_it_is_logged_as_non_fatal()
    {
        using var context = TestCorrelator.CreateContext();

        //Arrange / Act - a fatal error latches the consumer as dead; a following non-fatal error must
        //still be logged at its own (non-fatal) level, not escalated to fatal just because the
        //consumer is now latched. Latching the state must not couple the logging decision to it.
        _consumer.HandleError(new Error(ErrorCode.Local_Fatal, "a fatal consumer error", isFatal: true));
        _consumer.HandleError(new Error(ErrorCode.Local_TimedOut, "an idle socket non fatal timeout", isFatal: false));

        //Assert - the non-fatal error was logged at Warning (NonFatalError), not Error (FatalError)
        var nonFatalEvent = TestCorrelator.GetLogEventsFromCurrentContext()
            .Single(e => e.RenderMessage().Contains("an idle socket non fatal timeout"));
        await Assert.That(nonFatalEvent.Level).IsEqualTo(LogEventLevel.Warning);
    }

    public void Dispose()
    {
        _consumer?.Dispose();
    }
}