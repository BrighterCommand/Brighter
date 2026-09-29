using System;
using Confluent.Kafka;
using Paramore.Brighter.MessagingGateway.Kafka;


namespace Paramore.Brighter.Kafka.Tests.MessagingGateway.Reactor;

[Property("Category", "Kafka")]
[System.Obsolete] //
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

    [Test]
    public async System.Threading.Tasks.Task When_round_tripping_a_timestamp_should_preserve_the_instant_across_hops()
    {
        //act - first hop: the original send
        Headers firstHopHeaders = _builder.Build(_message);
        Message firstHop = new KafkaMessageCreator().CreateMessage(ConsumeResultFor(firstHopHeaders));

        //assert - the instant, and its UTC wall-clock, survive the hop
        await Assert.That(firstHop.Header.TimeStamp).IsEqualTo(s_timeStamp);
        await Assert.That(firstHop.Header.TimeStamp.ToUniversalTime().DateTime).IsEqualTo(s_timeStamp.ToUniversalTime().DateTime);

        //assert - what we read is anchored to UTC, not re-stamped with the host's offset
        await Assert.That(firstHop.Header.TimeStamp.Offset).IsEqualTo(TimeSpan.Zero);

        //act - second hop: re-publishing what we read, as a requeue does
        Headers secondHopHeaders = _builder.Build(firstHop);

        //assert - a re-publish is idempotent on the wire, so drift cannot accumulate over hops
        await Assert.That(secondHopHeaders.GetLastBytes(HeaderNames.TIMESTAMP)).IsEquivalentTo(firstHopHeaders.GetLastBytes(HeaderNames.TIMESTAMP), TUnit.Assertions.Enums.CollectionOrdering.Matching);
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
