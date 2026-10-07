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
public class When_error_log_level_is_null_should_log_fatal_at_error_and_non_fatal_at_warning : IDisposable
{
    private readonly KafkaMessageConsumer _consumer;

    public When_error_log_level_is_null_should_log_fatal_at_error_and_non_fatal_at_warning()
    {
        //Arrange - no ErrorLogLevel hook is configured, so the default classification must apply
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
    public void When_no_error_log_level_is_configured_fatal_errors_log_at_error_and_non_fatal_at_warning()
    {
        using var context = TestCorrelator.CreateContext();

        //Act
        _consumer.HandleError(new Error(ErrorCode.Local_Fatal, "a fatal consumer error", isFatal: true));
        _consumer.HandleError(new Error(ErrorCode.Local_TimedOut, "an idle socket non fatal timeout", isFatal: false));

        //Assert - the default behaviour is preserved: fatal at Error, non-fatal at Warning
        var events = TestCorrelator.GetLogEventsFromCurrentContext().ToList();

        var fatalEvent = events.Single(e => e.RenderMessage().Contains("a fatal consumer error"));
        Assert.Equal(LogEventLevel.Error, fatalEvent.Level);

        var nonFatalEvent = events.Single(e => e.RenderMessage().Contains("an idle socket non fatal timeout"));
        Assert.Equal(LogEventLevel.Warning, nonFatalEvent.Level);
    }

    public void Dispose()
    {
        _consumer?.Dispose();
    }
}
