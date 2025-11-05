using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Sns;

[Collection("MessagingGateway")]
public class SnsFifoProactorTests : SnsProactorTests
{
    protected override SqsType TopicType => SqsType.Fifo;
    protected override bool HasSupportToPartitionKey => true;

    protected override Message CreateMessage(RoutingKey routingKey, bool setTrace = true)
    {
        var message = base.CreateMessage(routingKey, setTrace);
        message.Header.PartitionKey = new PartitionKey(Uuid.New().ToString("N"));
        return message;
    }

    protected override void AssertMessageAreEquals(Message expected, Message received)
    {
        Assert.Equal(expected.Header.MessageType, received.Header.MessageType);
        Assert.Equal(expected.Header.ContentType, received.Header.ContentType);
        Assert.Equal(expected.Header.CorrelationId, received.Header.CorrelationId);
        Assert.Equal(expected.Header.DataSchema, received.Header.DataSchema);
        Assert.Equal(expected.Header.MessageId, received.Header.MessageId);
        Assert.Equal(expected.Header.PartitionKey, received.Header.PartitionKey);
        Assert.Equal(expected.Header.ReplyTo, received.Header.ReplyTo);
        Assert.Equal(expected.Header.Subject, received.Header.Subject);
        Assert.Equal(expected.Header.SpecVersion, received.Header.SpecVersion);
        Assert.Equal(expected.Header.Source, received.Header.Source);
        Assert.Equal($"{expected.Header.Topic}.fifo", received.Header.Topic);
        Assert.Equal(expected.Header.TimeStamp.ToString("yyyy-MM-ddTHH:mm:ss"), received.Header.TimeStamp.ToString("yyyy-MM-ddTHH:mm:ss"));
        Assert.Equal(expected.Header.Type, received.Header.Type);
        Assert.Equal(expected.Body.Value, received.Body.Value);
        Assert.Equal(expected.Header.TraceParent, received.Header.TraceParent);
        Assert.Equal(expected.Header.TraceState, received.Header.TraceState);
        Assert.Equal(expected.Header.Baggage, received.Header.Baggage);
    }
}
