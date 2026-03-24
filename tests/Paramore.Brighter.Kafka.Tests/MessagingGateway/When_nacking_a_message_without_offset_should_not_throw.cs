using System;
using Confluent.Kafka;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway;

[Trait("Category", "Kafka")]
[Collection("Kafka")]
public class When_nacking_a_message_without_offset_should_not_throw : IDisposable
{
    private readonly KafkaMessageConsumer _consumer;

    public When_nacking_a_message_without_offset_should_not_throw()
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
    public void When_message_has_no_partition_offset_in_bag_should_not_throw()
    {
        //Arrange - a message with no PARTITION_OFFSET in the header bag
        var message = new Message(
            new MessageHeader("test-id", new RoutingKey("test.topic"), MessageType.MT_COMMAND),
            new MessageBody("test body")
        );

        //Act & Assert - should return without throwing
        var exception = Record.Exception(() => _consumer.Nack(message));
        Assert.Null(exception);
    }

    [Fact]
    public void When_message_has_wrong_type_for_partition_offset_should_not_throw()
    {
        //Arrange - a message with PARTITION_OFFSET set to the wrong type
        var message = new Message(
            new MessageHeader("test-id", new RoutingKey("test.topic"), MessageType.MT_COMMAND),
            new MessageBody("test body")
        );
        message.Header.Bag[HeaderNames.PARTITION_OFFSET] = "not-a-TopicPartitionOffset";

        //Act & Assert - should return without throwing
        var exception = Record.Exception(() => _consumer.Nack(message));
        Assert.Null(exception);
    }

    public void Dispose()
    {
        _consumer?.Dispose();
    }
}
