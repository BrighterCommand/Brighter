using System;
using System.Collections.Generic;
using System.Globalization;
using Confluent.Kafka;
using FluentAssertions;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway.Reactor;

[Trait("Category", "Kafka")]
[Collection("Kafka")]   //
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
                contentType: "application/octet",
                partitionKey: "mykey"
            ),
            new MessageBody("test content")
        );

        message.Header.Delayed = TimeSpan.FromMilliseconds(500);
        message.Header.HandledCount = 2;

        Dictionary<string,object> bag = message.Header.Bag;
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
        headers.GetLastBytes(HeaderNames.MESSAGE_TYPE).Should().Equal(message.Header.MessageType.ToString().ToByteArray());
        headers.GetLastBytes(HeaderNames.MESSAGE_ID).Should().Equal(message.Header.MessageId.ToString().ToByteArray());
        headers.GetLastBytes(HeaderNames.TOPIC).Should().Equal(message.Header.Topic.Value.ToByteArray());
        headers.GetLastBytes(HeaderNames.TIMESTAMP).Should().Equal(message.Header.TimeStamp.DateTime.ToString(CultureInfo.InvariantCulture).ToByteArray());
        headers.GetLastBytes(HeaderNames.CORRELATION_ID).Should()
            .Equal(message.Header.CorrelationId.ToString().ToByteArray());
        headers.GetLastBytes(HeaderNames.PARTITIONKEY).Should().Equal(message.Header.PartitionKey.ToByteArray());
        headers.GetLastBytes(HeaderNames.CONTENT_TYPE).Should().Equal(message.Header.ContentType.ToByteArray());
        headers.GetLastBytes(HeaderNames.REPLY_TO).Should().Equal(message.Header.ReplyTo.ToByteArray());
        headers.GetLastBytes(HeaderNames.DELAYED_MILLISECONDS).Should().Equal(message.Header.Delayed.TotalMilliseconds.ToString().ToByteArray());
        headers.GetLastBytes(HeaderNames.HANDLED_COUNT).Should()
            .Equal(message.Header.HandledCount.ToString().ToByteArray());    

        //bag properties    
        headers.GetLastBytes("myguid").Should().Equal(bag["myguid"].ToString().ToByteArray());
        headers.GetLastBytes("mystring").Should().Equal(bag["mystring"].ToString().ToByteArray());
        headers.GetLastBytes("myint").Should().Equal(bag["myint"].ToString().ToByteArray());
        headers.GetLastBytes("mydouble").Should().Equal(bag["mydouble"].ToString().ToByteArray());
        headers.GetLastBytes("mydatetime").Should().Equal(myDateTime.ToByteArray());
    }
}
