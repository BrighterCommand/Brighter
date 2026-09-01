using System;
using System.Linq;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.MessagingGateway.Kafka;
using Serilog.Sinks.TestCorrelator;
using Xunit;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway;

[Trait("Category", "Kafka")]
[Collection("Kafka")]
public class When_error_log_level_returns_none_should_suppress_logging : IDisposable
{
    private readonly KafkaMessageConsumer _consumer;

    public When_error_log_level_returns_none_should_suppress_logging()
    {
        //Arrange - the hook returns LogLevel.None for the error code we want to silence
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
            errorLogLevel: error => error.Code == ErrorCode.Local_TimedOut ? LogLevel.None : LogLevel.Warning,
            loggerFactory: Initializer.TestLoggerFactory);
    }

    [Fact]
    public void When_the_error_log_level_returns_none_the_error_is_not_logged()
    {
        using var context = TestCorrelator.CreateContext();

        //Act
        _consumer.HandleError(new Error(ErrorCode.Local_TimedOut, "an idle socket non fatal timeout", isFatal: false));

        //Assert - no log event is written for the suppressed error
        var matchingEvents = TestCorrelator.GetLogEventsFromCurrentContext()
            .Count(e => e.RenderMessage().Contains("an idle socket non fatal timeout"));
        Assert.Equal(0, matchingEvents);
    }

    public void Dispose()
    {
        _consumer?.Dispose();
    }
}
