using System;
using System.Collections.Generic;
using System.Globalization;
using Confluent.Kafka;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway.Reactor;

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
        Assert.Equal(message.Id, readMessage.Id);
        Assert.Equal(message.Header.MessageType, readMessage.Header.MessageType);
        Assert.Equal(message.Header.MessageId, readMessage.Header.MessageId);
        Assert.Equal(message.Header.CorrelationId, readMessage.Header.CorrelationId);
        Assert.Equal(message.Header.ContentType, readMessage.Header.ContentType);
        Assert.Equal(message.Header.Topic, readMessage.Header.Topic);
        Assert.Equal(message.Header.Delayed, readMessage.Header.Delayed);
        Assert.Equal(message.Header.HandledCount, readMessage.Header.HandledCount);
        Assert.Equal(message.Header.TimeStamp.DateTime, readMessage.Header.TimeStamp.DateTime, TimeSpan.FromSeconds(5));

        //NOTE: Because we can only coerce the byte[] to a string for a unknown bag key, coercing to a specific
        //type has to be done by the user of the bag.
        Assert.Equal(bag["myguid"].ToString(), readMessage.Header.Bag["myguid"]);
        Assert.Equal(bag["mystring"].ToString(), readMessage.Header.Bag["mystring"]);
        Assert.Equal(bag["myint"].ToString(), readMessage.Header.Bag["myint"]);
        Assert.Equal(bag["mydouble"].ToString(), readMessage.Header.Bag["mydouble"]);
        Assert.Equal(((DateTime)bag["mydatetime"]).ToString(CultureInfo.InvariantCulture), readMessage.Header.Bag["mydatetime"]);
    }
}
