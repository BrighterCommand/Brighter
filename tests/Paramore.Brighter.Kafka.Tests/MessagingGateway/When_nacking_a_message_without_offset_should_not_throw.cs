using System;
using Confluent.Kafka;
using Paramore.Brighter.MessagingGateway.Kafka;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway;

[Category("Kafka")]
public class When_nacking_a_message_without_offset_should_not_throw : IDisposable
{
    private readonly KafkaMessageConsumer _consumer;
    private readonly RoutingKey _topic = new(Guid.NewGuid().ToString("N"));

    public When_nacking_a_message_without_offset_should_not_throw()
    {
        var groupId = Guid.NewGuid().ToString("N");

        _consumer = new KafkaMessageConsumer(
            new KafkaMessagingGatewayConfiguration
            {
                Name = "test", BootStrapServers = ["localhost:9092"]
            },
            routingKey: _topic,
            groupId: groupId,
            offsetDefault: AutoOffsetReset.Earliest,
            numPartitions: 1,
            replicationFactor: 1,
            makeChannels: OnMissingChannel.Assume
        );
    }

    [Test]
    public async Task When_message_has_no_partition_offset_in_bag_should_not_throw()
    {
        //Arrange - a message with no PARTITION_OFFSET in the header bag
        var message = new Message(
            new MessageHeader("test-id", _topic, MessageType.MT_COMMAND),
            new MessageBody("test body")
        );

        //Act & Assert - should return without throwing
        await Assert.That(() => _consumer.Nack(message)).ThrowsNothing();
    }

    [Test]
    public async Task When_message_has_wrong_type_for_partition_offset_should_not_throw()
    {
        //Arrange - a message with PARTITION_OFFSET set to the wrong type
        var message = new Message(
            new MessageHeader("test-id", _topic, MessageType.MT_COMMAND),
            new MessageBody("test body")
        );
        message.Header.Bag[HeaderNames.PARTITION_OFFSET] = "not-a-TopicPartitionOffset";

        //Act & Assert - should return without throwing
        await Assert.That(() => _consumer.Nack(message)).ThrowsNothing();
    }

    public void Dispose()
    {
        _consumer?.Dispose();
    }
}

