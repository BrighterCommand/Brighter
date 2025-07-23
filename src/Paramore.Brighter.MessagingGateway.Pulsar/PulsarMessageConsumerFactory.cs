using DotPulsar.Extensions;

namespace Paramore.Brighter.MessagingGateway.Pulsar;

/// <summary>
/// Factory for creating Pulsar message consumers (synchronous and asynchronous) for Brighter's message processing pipeline.
/// </summary>
/// <param name="connection">The connection gateway to Apache Pulsar used for creating consumers.</param>
public class PulsarMessageConsumerFactory(PulsarMessagingGatewayConnection connection) : IAmAMessageConsumerFactory
{
    /// <inheritdoc />
    public IAmAMessageConsumerSync Create(Subscription subscription) 
        => CreatePulsarConsumer(subscription);

    /// <inheritdoc />
    public IAmAMessageConsumerAsync CreateAsync(Subscription subscription)
        => CreatePulsarConsumer(subscription);

    private PulsarMessageConsumer CreatePulsarConsumer(Subscription subscription)
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
        return new PulsarMessageConsumer(consumer);
    }
}
