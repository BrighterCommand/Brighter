using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Mime;
using Confluent.Kafka;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.MessagingGateway.Kafka;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway;

public class KafkaHeaderUtf8EncodingTests
{
    [Test]
    public async Task When_header_bag_contains_unicode_should_round_trip_correctly()
    {
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

        var readMessage = new KafkaMessageCreator().CreateMessage(consumeResult);

        await Assert.That(readMessage.Header.Bag["unicode_key"]).IsEqualTo(unicodeValue);
    }

    [Test]
    public async Task When_standard_headers_round_trip_should_preserve_values()
    {
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

        var readMessage = new KafkaMessageCreator().CreateMessage(consumeResult);

        await Assert.That(readMessage.Header.MessageType).IsEqualTo(message.Header.MessageType);
        await Assert.That(readMessage.Header.Topic).IsEqualTo(message.Header.Topic);
        await Assert.That(readMessage.Header.CorrelationId).IsEqualTo(message.Header.CorrelationId);
        await Assert.That(readMessage.Header.HandledCount).IsEqualTo(message.Header.HandledCount);
    }
}
