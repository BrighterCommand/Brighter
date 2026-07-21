using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Mime;
using Confluent.Kafka;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.MessagingGateway.Kafka;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway.Reactor;

[Category("Kafka")]
public class KafkaHeaderToBrighterTests  
{
    [Test]
    public async Task When_converting_kafkaheader_to_brighterheader()
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
                contentType: new ContentType(MediaTypeNames.Application.Octet){CharSet = CharacterEncoding.UTF8.FromCharacterEncoding()},
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
        await Assert.That(readMessage.Id).IsEqualTo(message.Id);
        await Assert.That(readMessage.Header.MessageType).IsEqualTo(message.Header.MessageType);
        await Assert.That(readMessage.Header.MessageId).IsEqualTo(message.Header.MessageId);
        await Assert.That(readMessage.Header.CorrelationId).IsEqualTo(message.Header.CorrelationId);
        await Assert.That(readMessage.Header.ContentType).IsEqualTo(message.Header.ContentType);
        await Assert.That(readMessage.Header.Topic).IsEqualTo(message.Header.Topic);
        await Assert.That(readMessage.Header.Delayed).IsEqualTo(message.Header.Delayed);
        await Assert.That(readMessage.Header.HandledCount).IsEqualTo(message.Header.HandledCount);
        await Assert.That(readMessage.Header.TimeStamp).IsEqualTo(message.Header.TimeStamp).Within(TimeSpan.FromSeconds(5));

        //NOTE: Because we can only coerce the byte[] to a string for a unknown bag key, coercing to a specific
        //type has to be done by the user of the bag.
        await Assert.That(readMessage.Header.Bag["myguid"]).IsEqualTo(bag["myguid"].ToString());
        await Assert.That(readMessage.Header.Bag["mystring"]).IsEqualTo(bag["mystring"].ToString());
        await Assert.That(readMessage.Header.Bag["myint"]).IsEqualTo(bag["myint"].ToString());
        await Assert.That(readMessage.Header.Bag["mydouble"]).IsEqualTo(bag["mydouble"].ToString());
        await Assert.That(readMessage.Header.Bag["mydatetime"]).IsEqualTo(((DateTime)bag["mydatetime"]).ToString(CultureInfo.InvariantCulture));
    }
}

