using Paramore.Brighter.Base.Test.MessagingGateway;
using Paramore.Brighter.Base.Test.Requests;
using Paramore.Brighter.MessagingGateway.RocketMQ;
using Paramore.Brighter.RocketMQ.Tests.Utils;
using Xunit;

namespace Paramore.Brighter.RocketMQ.Tests.MessagingGateway.Reactor;

[Collection("MessagingGateway")]
public class RocketMqReactorTests : MessagingGatewayReactorTests<RocketMqPublication, RocketSubscription>
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

    protected override IAmAMessageProducerSync CreateProducer(RocketMqPublication publication)
    {
        var producers = new RocketMessageProducerFactory(GatewayFactory.CreateConnection(), [publication])
            .Create();
        var producer = producers.First().Value;
        return (IAmAMessageProducerSync)producer;
    }

    protected override IAmAChannelSync CreateChannel(RocketSubscription subscription)
    {
        var channel = new RocketMqChannelFactory(new RocketMessageConsumerFactory(GatewayFactory.CreateConnection()))
                .CreateSyncChannel(subscription);

        channel.Purge();
        return channel;
    }
}
