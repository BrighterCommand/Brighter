using System;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.MessagingGateway.Kafka;


namespace Paramore.Brighter.Kafka.Tests.MessagingGateway;

[Property("Category", "Kafka")]
[System.Obsolete]
public class When_error_log_level_suppresses_logs_should_still_latch_fatal_error : IDisposable
{
    private readonly KafkaMessageConsumer _consumer;

    public When_error_log_level_suppresses_logs_should_still_latch_fatal_error()
    {
        //Arrange - the hook suppresses every log entry; fatal handling must be unaffected
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
            errorLogLevel: _ => LogLevel.None
        );
    }

    [Test]
    public async System.Threading.Tasks.Task When_the_error_log_level_suppresses_logging_the_fatal_error_is_still_latched()
    {
        //Act - even though the hook suppresses the log call, the fatal error must still latch the consumer
        _consumer.HandleError(new Error(ErrorCode.Local_Fatal, "a fatal consumer error", isFatal: true));
        Exception? exception = null;
        try
        {
            _consumer.Receive(TimeSpan.Zero);
        }
        catch (Exception e)
        {
            exception = e;
        }

        //Assert - the latch fires purely from error.IsFatal, independent of the hook
        await Assert.That(exception).IsTypeOf<ChannelFailureException>();
    }

    public void Dispose()
    {
        _consumer?.Dispose();
    }
}
