using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Mime;
using Confluent.Kafka;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway.Reactor;

[Trait("Category", "Kafka")]
[Collection("Kafka")] //
public class KafkaDefaultMessageHeaderBuilderTests
{
    [Fact]
    public void When_converting_brighterheader_to_kafkaheader()
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
        Assert.Equal(message.Header.MessageType.ToString().ToByteArray(), headers.GetLastBytes(HeaderNames.MESSAGE_TYPE));
        Assert.Equal(message.Header.MessageId.Value.ToByteArray(), headers.GetLastBytes(HeaderNames.MESSAGE_ID));
        Assert.Equal(message.Header.Topic.Value.ToByteArray(), headers.GetLastBytes(HeaderNames.TOPIC));
        Assert.Equal(message.Header.TimeStamp.DateTime.ToString(CultureInfo.InvariantCulture).ToByteArray(), headers.GetLastBytes(HeaderNames.TIMESTAMP));
        Assert.Equal(message.Header.CorrelationId.Value.ToByteArray(), headers.GetLastBytes(HeaderNames.CORRELATION_ID));
        Assert.Equal(message.Header.PartitionKey.Value.ToByteArray(), headers.GetLastBytes(HeaderNames.PARTITIONKEY));
        Assert.Equal(message.Header.ContentType!.ToString().ToByteArray(), headers.GetLastBytes(HeaderNames.CONTENT_TYPE));
        Assert.Equal(message.Header.ReplyTo!.Value.ToByteArray(), headers.GetLastBytes(HeaderNames.REPLY_TO));
        Assert.Equal(message.Header.Delayed.TotalMilliseconds.ToString().ToByteArray(), headers.GetLastBytes(HeaderNames.DELAYED_MILLISECONDS));
        Assert.Equal(message.Header.HandledCount.ToString().ToByteArray(), headers.GetLastBytes(HeaderNames.HANDLED_COUNT));
        Assert.Equal(message.Header.Type.Value.ToByteArray(), headers.GetLastBytes(HeaderNames.CLOUD_EVENTS_TYPE));
        Assert.Equal(message.Header.Subject!.ToByteArray(), headers.GetLastBytes(HeaderNames.CLOUD_EVENTS_SUBJECT));
        Assert.Equal(message.Header.Source.ToString().ToByteArray(), headers.GetLastBytes(HeaderNames.CLOUD_EVENTS_SOURCE));
        Assert.Equal(message.Header.DataSchema!.ToString().ToByteArray(), headers.GetLastBytes(HeaderNames.CLOUD_EVENTS_DATA_SCHEMA));

        //bag properties
        Assert.Equal(bag["myguid"].ToString()!.ToByteArray(), headers.GetLastBytes("myguid"));
        Assert.Equal(bag["mystring"].ToString()!.ToByteArray(), headers.GetLastBytes("mystring"));
        Assert.Equal(bag["myint"].ToString()!.ToByteArray(), headers.GetLastBytes("myint"));
        Assert.Equal(bag["mydouble"].ToString()!.ToByteArray(), headers.GetLastBytes("mydouble"));
        Assert.Equal(myDateTime.ToByteArray(), headers.GetLastBytes("mydatetime"));

    }
}
