using System;

namespace Paramore.Brighter;

public class InMemoryChannelFactory(InternalBus internalBus, TimeProvider timeProvider) : IAmAChannelFactory
{
    public IAmAChannel CreateChannel(Subscription subscription)
    {
        return new Channel(
            subscription.ChannelName, 
            new InMemoryMessageConsumer(subscription.RoutingKey,internalBus, timeProvider, 1000),
            subscription.BufferSize
            );
    }
}
