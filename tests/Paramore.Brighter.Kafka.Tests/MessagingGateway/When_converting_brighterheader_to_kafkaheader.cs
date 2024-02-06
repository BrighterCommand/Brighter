using System;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway;

public class KafkaDefaultMessageHeaderBuilderTests 
{
    public KafkaDefaultMessageHeaderBuilderTests()
    {
        var message = new Message(
            new MessageHeader(
                messageId: Guid.NewGuid(),
                topic: "test",
                messageType: MessageType.MT_COMMAND,
                timeStamp: DateTime.UtcNow,
                correlationId: Guid.NewGuid()
            ),
            new MessageBody("test content")
        );
    }

    [Fact]
    public void When_converting_brighterheader_to_kafkaheader()
    {
        var builder = new KafkaDefaultMessageHeaderBuilder();

    }
}
