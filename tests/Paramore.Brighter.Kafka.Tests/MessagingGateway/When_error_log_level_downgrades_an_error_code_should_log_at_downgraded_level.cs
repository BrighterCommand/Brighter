using System;
using System.Linq;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.MessagingGateway.Kafka;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using Xunit;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway;

[Trait("Category", "Kafka")]
[Collection("Kafka")]
public class When_error_log_level_downgrades_an_error_code_should_log_at_downgraded_level : IDisposable
{
    private readonly KafkaMessageConsumer _consumer;

    public When_error_log_level_downgrades_an_error_code_should_log_at_downgraded_level()
    {
        //Arrange - the hook downgrades idle socket timeouts to Debug, leaving every other error at its default
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
            makeChannels: OnMissingChannel.Assume,
            errorLogLevel: error => error.Code == ErrorCode.Local_TimedOut ? LogLevel.Debug : LogLevel.Warning
        );
    }

    [Fact]
    public void When_the_error_log_level_downgrades_an_error_code_it_is_logged_at_that_level()
    {
        using var context = TestCorrelator.CreateContext();

        //Act
        _consumer.HandleError(new Error(ErrorCode.Local_TimedOut, "an idle socket non fatal timeout", isFatal: false));

        //Assert - the downgraded error is logged at Debug, not Warning
        var nonFatalEvent = TestCorrelator.GetLogEventsFromCurrentContext()
            .Single(e => e.RenderMessage().Contains("an idle socket non fatal timeout"));
        Assert.Equal(LogEventLevel.Debug, nonFatalEvent.Level);
    }

    public void Dispose()
    {
        _consumer?.Dispose();
    }
}
