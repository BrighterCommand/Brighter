using System;
using System.Collections.Generic;
using System.Text;
using Confluent.Kafka;
using FluentAssertions;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway;

public class KafkaDefaultMessageHeaderBuilderTests 
{
    [Fact]
    public void When_converting_brighterheader_to_kafkaheader()
    {
        var message = new Message(
            new MessageHeader(
                messageId: Guid.NewGuid(),
                topic: "test",
                messageType: MessageType.MT_COMMAND,
                timeStamp: DateTime.UtcNow,
                correlationId: Guid.NewGuid(),
                replyTo: "test",
                contentType: "application/octet",
                partitionKey: "mykey"
            ),
            new MessageBody("test content")
        );

        Dictionary<string,object> bag = message.Header.Bag;
        bag.Add("myguid", Guid.NewGuid());
        bag.Add("mystring", "string value");
        bag.Add("myint", 7);
        bag.Add("mydouble", 3.56);
        bag.Add("mybytearray", Encoding.UTF8.GetBytes("mybytes"));
        bag.Add("mydatetime", DateTime.UtcNow);
        bag.Add("mydateonly", DateOnly.FromDateTime(DateTime.UtcNow));
        
        var builder = new KafkaDefaultMessageHeaderBuilder();
        Headers headers = builder.Build(message);
        
        //known properties
        headers.GetLastBytes(HeaderNames.MESSAGE_TYPE).Should().Equal(message.Header.MessageType.ToString().ToByteArray());
        headers.GetLastBytes(HeaderNames.MESSAGE_ID).Should().Equal(message.Header.Id.ToString().ToByteArray());
        headers.GetLastBytes(HeaderNames.TOPIC).Should().Equal(message.Header.Topic.ToByteArray());
        headers.GetLastBytes(HeaderNames.TIMESTAMP).Should()
            .Equal(BitConverter.GetBytes(new DateTimeOffset(message.Header.TimeStamp).ToUnixTimeMilliseconds()));
        headers.GetLastBytes(HeaderNames.CORRELATION_ID).Should()
            .Equal(message.Header.CorrelationId.ToString().ToByteArray());
        headers.GetLastBytes(HeaderNames.PARTITIONKEY).Should().Equal(message.Header.PartitionKey.ToByteArray());
        headers.GetLastBytes(HeaderNames.CONTENT_TYPE).Should().Equal(message.Header.ContentType.ToByteArray());
        headers.GetLastBytes(HeaderNames.REPLY_TO).Should().Equal(message.Header.ReplyTo.ToByteArray());

        //bag properties    
        headers.GetLastBytes("myguid").Should().Equal(bag["myguid"].ToString().ToByteArray());
        headers.GetLastBytes("mystring").Should().Equal(bag["mystring"].ToString().ToByteArray());
        headers.GetLastBytes("myint").Should().Equal(BitConverter.GetBytes((int)bag["myint"]));
        headers.GetLastBytes("mydouble").Should().Equal(BitConverter.GetBytes((double)bag["mydouble"]));
        headers.GetLastBytes("mybytearray").Should().Equal((byte[])bag["mybytearray"]);
        headers.GetLastBytes("mydatetime").Should().Equal(((DateTime)bag["mydatetime"]).ToString().ToByteArray());
    }
}
