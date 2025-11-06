using Paramore.Brighter.MessagingGateway.RocketMQ;
using Xunit;

namespace Paramore.Brighter.RocketMQ.Tests.MessagingGateway.Proactor;

[Collection("MessagingGateway")]
public class RocketMqFifoProactorTests : RocketMqProactorTests
{
    protected override bool HasSupportToPartitionKey => true;

    protected override RoutingKey GetOrCreateRoutingKey(string testName = null!)
    {
        return new RoutingKey($"P{testName}");
    }

    protected override RocketMqPublication CreatePublication(RoutingKey routingKey)
    {
        var publication = base.CreatePublication(routingKey);
        publication.TopicType = TopicType.Fifo;
        return publication;
    }

    protected override Message CreateMessage(RoutingKey routingKey, bool setTrace = true)
    {
        var message = base.CreateMessage(routingKey, setTrace);
        message.Header.PartitionKey = new PartitionKey(Uuid.NewAsString());
        return message;
    }
}
