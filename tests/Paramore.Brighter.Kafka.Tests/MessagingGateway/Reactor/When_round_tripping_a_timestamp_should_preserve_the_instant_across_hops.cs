using System;
using Confluent.Kafka;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway.Reactor;

[Trait("Category", "Kafka")]
[Collection("Kafka")] //
public class KafkaTimeStampRoundTripTests
{
    //A timestamp deliberately *not* at UTC. If the writer drops the offset the reader cannot recover
    //the instant, so this fails on any host - we do not depend on the test host being in a non-UTC zone.
    private static readonly DateTimeOffset s_timeStamp = new(2024, 6, 15, 13, 45, 30, TimeSpan.FromHours(5));

    private readonly KafkaDefaultMessageHeaderBuilder _builder = new();
    private readonly Message _message;

    public KafkaTimeStampRoundTripTests()
    {
        //arrange
        _message = new Message(
            new MessageHeader(
                messageId: Guid.NewGuid().ToString(),
                topic: new RoutingKey("test"),
                messageType: MessageType.MT_COMMAND,
                timeStamp: s_timeStamp),
            new MessageBody("test content")
        );
    }

    [Fact]
    public void When_round_tripping_a_timestamp_should_preserve_the_instant_across_hops()
    {
        //act - first hop: the original send
        Headers firstHopHeaders = _builder.Build(_message);
        Message firstHop = new KafkaMessageCreator().CreateMessage(ConsumeResultFor(firstHopHeaders));

        //assert - the instant, and its UTC wall-clock, survive the hop
        Assert.Equal(s_timeStamp, firstHop.Header.TimeStamp);
        Assert.Equal(s_timeStamp.ToUniversalTime().DateTime, firstHop.Header.TimeStamp.ToUniversalTime().DateTime);

        //assert - what we read is anchored to UTC, not re-stamped with the host's offset
        Assert.Equal(TimeSpan.Zero, firstHop.Header.TimeStamp.Offset);

        //act - second hop: re-publishing what we read, as a requeue does
        Headers secondHopHeaders = _builder.Build(firstHop);

        //assert - a re-publish is idempotent on the wire, so drift cannot accumulate over hops
        Assert.Equal(firstHopHeaders.GetLastBytes(HeaderNames.TIMESTAMP),
            secondHopHeaders.GetLastBytes(HeaderNames.TIMESTAMP));
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
