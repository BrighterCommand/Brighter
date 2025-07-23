using DotPulsar.Extensions;

namespace Paramore.Brighter.MessagingGateway.Pulsar;

public class PulsarMessageConsumerFactory(PulsarMessagingGatewayConnection connection) : IAmAMessageConsumerFactory
{
    /// <inheritdoc />
    public IAmAMessageConsumerSync Create(Subscription subscription) 
        => CreatePulsarConsumer(subscription);

    /// <inheritdoc />
    public IAmAMessageConsumerAsync CreateAsync(Subscription subscription)
        => CreatePulsarConsumer(subscription);

    private PulsarConsumer CreatePulsarConsumer(Subscription subscription)
    {
        if (subscription is not PulsarSubscription pulsarSubscription)
        {
            throw new ConfigurationException("We expect PulsarSubscription or PulsarSubscription<T> as a parameter");
        }

        var client = connection.Create();
        var builder = client.NewConsumer(pulsarSubscription.Schema)
            .AllowOutOfOrderDeliver(pulsarSubscription.AllowOutOfOrderDeliver)
            .InitialPosition(pulsarSubscription.InitialPosition)
            .MessagePrefetchCount((uint)pulsarSubscription.BufferSize)
            .PriorityLevel(pulsarSubscription.PriorityLevel)
            .ReadCompacted(pulsarSubscription.ReadCompacted)
            .SubscriptionName(pulsarSubscription.ChannelName.Value)
            .SubscriptionType(pulsarSubscription.SubscriptionType)
            .Topic(pulsarSubscription.RoutingKey);
        
        pulsarSubscription.Configuration?.Invoke(builder);

        var consumer = builder.Create();
        return new PulsarConsumer(consumer);
    }
}
