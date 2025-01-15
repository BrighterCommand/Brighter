using System;
using System.Collections.Generic;
using System.Globalization;
using Confluent.Kafka;
using FluentAssertions;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway.Local.Reactor;

[Trait("Category", "Kafka")]
[Collection("Kafka")]   //
public class KafkaHeaderToBrighterTests  
{
    [Fact]
    public void When_converting_kafkaheader_to_brighterheader()
    {
        //arrange
        
        var message = new Message(
            new MessageHeader(
                messageId: Guid.NewGuid().ToString(),
                topic: new RoutingKey("test"),
                messageType: MessageType.MT_COMMAND,
                timeStamp: DateTimeOffset.UtcNow,
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
        bag.Add("myguid", Guid.NewGuid().ToString());
        bag.Add("mystring", "string value");
        bag.Add("myint", 7);
        bag.Add("mydouble", 3.56);
        bag.Add("mydatetime", DateTime.UtcNow);
        
        var builder = new KafkaDefaultMessageHeaderBuilder();
        Headers headers = builder.Build(message);

        var result = new ConsumeResult<string, byte[]>();
        result.Topic = "test";
        result.Message = new Message<string, byte[]>
        {
            Headers = headers, 
            Key = message.Id.ToString(), 
            Value = "test content"u8.ToArray()
        };

        //act
        var readMessage = new KafkaMessageCreator().CreateMessage(result);

        //assert
        readMessage.Id.Should().Be(message.Id);
        readMessage.Header.MessageType.Should().Be(message.Header.MessageType);
        readMessage.Header.MessageId.Should().Be(message.Header.MessageId);
        readMessage.Header.CorrelationId.Should().Be(message.Header.CorrelationId);
        readMessage.Header.ContentType.Should().Be(message.Header.ContentType);
        readMessage.Header.Topic.Should().Be(message.Header.Topic);
        readMessage.Header.Delayed.Should().Be(message.Header.Delayed);
        readMessage.Header.HandledCount.Should().Be(message.Header.HandledCount);
        readMessage.Header.TimeStamp.DateTime.ToString(CultureInfo.InvariantCulture).Should().Be(message.Header.TimeStamp.DateTime.ToString(CultureInfo.InvariantCulture));            
        
        //NOTE: Because we can only coerce the byte[] to a string for a unknown bag key, coercing to a specific
        //type has to be done by the user of the bag.
        readMessage.Header.Bag["myguid"].Should().Be(bag["myguid"].ToString());
        readMessage.Header.Bag["mystring"].Should().Be(bag["mystring"].ToString());
        readMessage.Header.Bag["myint"].Should().Be(bag["myint"].ToString());
        readMessage.Header.Bag["mydouble"].Should().Be(bag["mydouble"].ToString());
        readMessage.Header.Bag["mydatetime"].Should().Be(((DateTime)bag["mydatetime"]).ToString(CultureInfo.InvariantCulture));
    }
}
