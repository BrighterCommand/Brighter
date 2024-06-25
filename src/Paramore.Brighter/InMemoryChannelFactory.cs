using System;

namespace Paramore.Brighter;

public class InMemoryChannelFactory(InternalBus internalBus, TimeProvider timeProvider, int ackTimeoutMs = 1000) : IAmAChannelFactory
{
    public IAmAChannel CreateChannel(Subscription subscription)
    {
        return new Channel(
            subscription.ChannelName, 
            new InMemoryMessageConsumer(subscription.RoutingKey,internalBus, timeProvider, ackTimeoutMs),
            subscription.BufferSize
            );
    }
}
