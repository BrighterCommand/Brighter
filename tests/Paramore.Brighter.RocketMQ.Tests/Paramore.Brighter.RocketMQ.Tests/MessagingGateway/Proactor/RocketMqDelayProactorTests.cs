using Paramore.Brighter.MessagingGateway.RocketMQ;
using Xunit;

namespace Paramore.Brighter.RocketMQ.Tests.MessagingGateway.Proactor;

[Collection("MessagingGateway")]
public class RocketMqDelayProactorTests : RocketMqProactorTests
{
    protected override bool HasSupportToDelayedMessages => true;

    protected override RoutingKey GetOrCreateRoutingKey(string testName = null!)
    {
        return new RoutingKey($"D{testName}");
    }

    protected override RocketMqPublication CreatePublication(RoutingKey routingKey)
    {
        var publication = base.CreatePublication(routingKey);
        publication.TopicType = TopicType.Delay;
        return publication;
    }
}
