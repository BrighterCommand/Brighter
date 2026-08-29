using System;
using System.Globalization;
using Confluent.Kafka;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway.Reactor;

[Trait("Category", "Kafka")]
[Collection("Kafka")] //
public class KafkaLegacyTimeStampFormatTests
{
    //The offset-less invariant-culture format written by producers on the old code, and the UTC
    //instant it was meant to carry.
    private static readonly DateTimeOffset s_utcTimeStamp = new(2024, 6, 15, 8, 45, 30, TimeSpan.Zero);

    private readonly Headers _headers;

    public KafkaLegacyTimeStampFormatTests()
    {
        //arrange
        var message = new Message(
            new MessageHeader(
                messageId: Guid.NewGuid().ToString(),
                topic: new RoutingKey("test"),
                messageType: MessageType.MT_COMMAND,
                timeStamp: s_utcTimeStamp),
            new MessageBody("test content")
        );

        _headers = new KafkaDefaultMessageHeaderBuilder().Build(message);
        _headers.Remove(HeaderNames.TIMESTAMP);
        _headers.Add(HeaderNames.TIMESTAMP,
            s_utcTimeStamp.UtcDateTime.ToString(CultureInfo.InvariantCulture).ToByteArray());
    }

    [Fact]
    public void When_reading_a_timestamp_from_an_older_producer_should_treat_it_as_utc()
    {
        //act
        Message read = new KafkaMessageCreator().CreateMessage(ConsumeResultFor(_headers));

        //assert - an offset-less value is taken as UTC and left there, not converted to host-local time
        Assert.Equal(s_utcTimeStamp, read.Header.TimeStamp);
        Assert.Equal(TimeSpan.Zero, read.Header.TimeStamp.Offset);
    }

    private static ConsumeResult<string, byte[]> ConsumeResultFor(Headers headers)
        => new()
        {
            Topic = "test",
            Message = new Message<string, byte[]>
            {
                Headers = headers, Key = Guid.NewGuid().ToString(), Value = "test content"u8.ToArray()
            }
        };
}
