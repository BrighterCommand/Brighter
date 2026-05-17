using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Mime;
using Confluent.Kafka;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway;

public class KafkaHeaderUtf8EncodingTests
{
    [Fact]
    public void When_header_bag_contains_unicode_should_round_trip_correctly()
    {
        // Arrange — a bag value with non-ASCII characters
        var message = new Message(
            new MessageHeader(
                messageId: Guid.NewGuid().ToString(),
                topic: new RoutingKey("test"),
                messageType: MessageType.MT_EVENT,
                timeStamp: DateTimeOffset.UtcNow
            ),
            new MessageBody("test content")
        );

        var unicodeValue = "café résumé naïve";
        message.Header.Bag.Add("unicode_key", unicodeValue);

        var builder = new KafkaDefaultMessageHeaderBuilder();
        var headers = builder.Build(message);

        var consumeResult = new ConsumeResult<string, byte[]>
        {
            Topic = "test",
            Message = new Message<string, byte[]>
            {
                Headers = headers,
                Key = message.Id.ToString(),
                Value = "test content"u8.ToArray()
            }
        };

        // Act
        var readMessage = new KafkaMessageCreator().CreateMessage(consumeResult);

        // Assert — the non-ASCII characters survive the round-trip
        Assert.Equal(unicodeValue, readMessage.Header.Bag["unicode_key"]);
    }

    [Fact]
    public void When_standard_headers_round_trip_should_preserve_values()
    {
        // Arrange
        var message = new Message(
            new MessageHeader(
                messageId: Guid.NewGuid().ToString(),
                topic: new RoutingKey("test.topic"),
                messageType: MessageType.MT_COMMAND,
                timeStamp: DateTimeOffset.UtcNow,
                correlationId: Guid.NewGuid().ToString(),
                replyTo: new RoutingKey("reply.topic"),
                contentType: new ContentType(MediaTypeNames.Application.Json)
                    { CharSet = CharacterEncoding.UTF8.FromCharacterEncoding() }
            ),
            new MessageBody("test content")
        );

        message.Header.HandledCount = 3;

        var builder = new KafkaDefaultMessageHeaderBuilder();
        var headers = builder.Build(message);

        var consumeResult = new ConsumeResult<string, byte[]>
        {
            Topic = "test.topic",
            Message = new Message<string, byte[]>
            {
                Headers = headers,
                Key = message.Id.ToString(),
                Value = "test content"u8.ToArray()
            }
        };

        // Act
        var readMessage = new KafkaMessageCreator().CreateMessage(consumeResult);

        // Assert — standard Brighter headers round-trip correctly
        Assert.Equal(message.Header.MessageType, readMessage.Header.MessageType);
        Assert.Equal(message.Header.Topic, readMessage.Header.Topic);
        Assert.Equal(message.Header.CorrelationId, readMessage.Header.CorrelationId);
        Assert.Equal(message.Header.HandledCount, readMessage.Header.HandledCount);
    }
}
