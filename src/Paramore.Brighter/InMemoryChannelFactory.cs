using System;

namespace Paramore.Brighter;

public class InMemoryChannelFactory(InternalBus internalBus, TimeProvider timeProvider, TimeSpan? ackTimeout = null) : IAmAChannelFactory
{
    public IAmAChannelSync CreateSyncChannel(Subscription subscription)
    {
        return new Channel(
            subscription.ChannelName, 
            subscription.RoutingKey, 
            new InMemoryMessageConsumer(subscription.RoutingKey,internalBus, timeProvider, ackTimeout),
            subscription.BufferSize
            );
    }

    public IAmAChannelAsync CreateAsyncChannel(Subscription subscription)
    {
        return new ChannelAsync(
            subscription.ChannelName, 
            subscription.RoutingKey, 
            new InMemoryMessageConsumer(subscription.RoutingKey,internalBus, timeProvider, ackTimeout),
            subscription.BufferSize
            );
    }
}
