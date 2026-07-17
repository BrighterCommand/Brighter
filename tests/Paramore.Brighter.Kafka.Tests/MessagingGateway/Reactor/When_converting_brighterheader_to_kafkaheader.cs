using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Mime;
using Confluent.Kafka;
using Paramore.Brighter.MessagingGateway.Kafka;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway.Reactor;

[Category("Kafka")]
public class KafkaDefaultMessageHeaderBuilderTests
{
    [Test]
    public async Task When_converting_brighterheader_to_kafkaheader()
    {
        //arrange
        var message = new Message(
            new MessageHeader(
                messageId: Guid.NewGuid().ToString(),
                topic: new RoutingKey("test"),
                messageType: MessageType.MT_COMMAND,
                timeStamp: DateTime.UtcNow,
                correlationId: Guid.NewGuid().ToString(),
                replyTo: new RoutingKey("test"),
                contentType: new ContentType(MediaTypeNames.Application.Octet),
                partitionKey: "mykey"
            )
            {
                Type = new CloudEventsType($"Type{Guid.NewGuid():N}"),
                Subject = $"Subject{Guid.NewGuid():N}",
                Source = new Uri($"/component/{Guid.NewGuid()}", UriKind.RelativeOrAbsolute),
                DataSchema = new Uri("https://example.com/storage/tenant/container", UriKind.RelativeOrAbsolute)
            },
            new MessageBody("test content")
        );

        message.Header.Delayed = TimeSpan.FromMilliseconds(500);
        message.Header.HandledCount = 2;

        Dictionary<string, object> bag = message.Header.Bag;
        bag.Add("myguid", Guid.NewGuid());
        bag.Add("mystring", "string value");
        bag.Add("myint", 7);
        bag.Add("mydouble", 3.56);
        var myDateTime = DateTimeOffset.UtcNow.DateTime.ToString(CultureInfo.InvariantCulture);
        bag.Add("mydatetime", myDateTime);

        //act
        var builder = new KafkaDefaultMessageHeaderBuilder();
        Headers headers = builder.Build(message);

        //assert

        //known properties
        await Assert.That(headers.GetLastBytes(HeaderNames.MESSAGE_TYPE)).IsEquivalentTo(message.Header.MessageType.ToString().ToByteArray());
        await Assert.That(headers.GetLastBytes(HeaderNames.MESSAGE_ID)).IsEquivalentTo(message.Header.MessageId.Value.ToByteArray());
        await Assert.That(headers.GetLastBytes(HeaderNames.TOPIC)).IsEquivalentTo(message.Header.Topic.Value.ToByteArray());
        await Assert.That(headers.GetLastBytes(HeaderNames.TIMESTAMP)).IsEquivalentTo(message.Header.TimeStamp.DateTime.ToString(CultureInfo.InvariantCulture).ToByteArray());
        await Assert.That(headers.GetLastBytes(HeaderNames.CORRELATION_ID)).IsEquivalentTo(message.Header.CorrelationId.Value.ToByteArray());
        await Assert.That(headers.GetLastBytes(HeaderNames.PARTITIONKEY)).IsEquivalentTo(message.Header.PartitionKey.Value.ToByteArray());
        await Assert.That(headers.GetLastBytes(HeaderNames.CONTENT_TYPE)).IsEquivalentTo(message.Header.ContentType!.ToString().ToByteArray());
        await Assert.That(headers.GetLastBytes(HeaderNames.REPLY_TO)).IsEquivalentTo(message.Header.ReplyTo!.Value.ToByteArray());
        await Assert.That(headers.GetLastBytes(HeaderNames.DELAYED_MILLISECONDS)).IsEquivalentTo(message.Header.Delayed.TotalMilliseconds.ToString().ToByteArray());
        await Assert.That(headers.GetLastBytes(HeaderNames.HANDLED_COUNT)).IsEquivalentTo(message.Header.HandledCount.ToString().ToByteArray());
        await Assert.That(headers.GetLastBytes(HeaderNames.CLOUD_EVENTS_TYPE)).IsEquivalentTo(message.Header.Type.Value.ToByteArray());
        await Assert.That(headers.GetLastBytes(HeaderNames.CLOUD_EVENTS_SUBJECT)).IsEquivalentTo(message.Header.Subject!.ToByteArray());
        await Assert.That(headers.GetLastBytes(HeaderNames.CLOUD_EVENTS_SOURCE)).IsEquivalentTo(message.Header.Source.ToString().ToByteArray());
        await Assert.That(headers.GetLastBytes(HeaderNames.CLOUD_EVENTS_DATA_SCHEMA)).IsEquivalentTo(message.Header.DataSchema!.ToString().ToByteArray());

        //bag properties
        await Assert.That(headers.GetLastBytes("myguid")).IsEquivalentTo(bag["myguid"].ToString()!.ToByteArray());
        await Assert.That(headers.GetLastBytes("mystring")).IsEquivalentTo(bag["mystring"].ToString()!.ToByteArray());
        await Assert.That(headers.GetLastBytes("myint")).IsEquivalentTo(bag["myint"].ToString()!.ToByteArray());
        await Assert.That(headers.GetLastBytes("mydouble")).IsEquivalentTo(bag["mydouble"].ToString()!.ToByteArray());
        await Assert.That(headers.GetLastBytes("mydatetime")).IsEquivalentTo(myDateTime.ToByteArray());

    }
}

