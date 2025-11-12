using Paramore.Brighter.Base.Test.MessagingGateway;
using Paramore.Brighter.Base.Test.MessagingGateway.Proactor;
using Paramore.Brighter.Base.Test.Requests;
using Paramore.Brighter.MessagingGateway.RocketMQ;
using Paramore.Brighter.RocketMQ.Tests.Utils;
using Xunit;

namespace Paramore.Brighter.RocketMQ.Tests.MessagingGateway.Proactor;

[Collection("MessagingGateway")]
public class RocketMqProactorTests : MessagingGatewayProactorTests<RocketMqPublication, RocketSubscription>
{
    protected override RoutingKey GetOrCreateRoutingKey(string testName = null!)
    {
        return new RoutingKey(testName);
    }

    protected override RocketMqPublication CreatePublication(RoutingKey routingKey)
    {
        return new RocketMqPublication
        {
            Topic = routingKey, 
            MakeChannels = OnMissingChannel.Assume
        };
    }

    protected override RocketSubscription CreateSubscription(RoutingKey routingKey, ChannelName channelName,
        OnMissingChannel makeChannel = OnMissingChannel.Create, bool setupDeadLetterQueue = false)
    {
        return new RocketMqSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(Uuid.New().ToString("N")),
            routingKey: routingKey,
            channelName: channelName,
            consumerGroup: $"ConsumerGroup{Uuid.New():N}",
            messagePumpType: MessagePumpType.Proactor,
            requeueCount: 32);
    }

    protected override async Task<IAmAMessageProducerAsync> CreateProducerAsync(RocketMqPublication publication, CancellationToken cancellationToken = default)
    {
        var producers = await new RocketMessageProducerFactory(GatewayFactory.CreateConnection(), [publication])
            .CreateAsync();
        var producer = producers.First().Value;
        return (IAmAMessageProducerAsync)producer;
    }

    protected override async Task<IAmAChannelAsync> CreateChannelAsync(RocketSubscription subscription, CancellationToken cancellationToken = default)
    {
        var channel = await new RocketMqChannelFactory(new RocketMessageConsumerFactory(GatewayFactory.CreateConnection()))
                .CreateAsyncChannelAsync(subscription, cancellationToken);

        await channel.PurgeAsync(cancellationToken);
        return channel;
    }
}
