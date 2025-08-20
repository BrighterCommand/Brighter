using System.Collections.Concurrent;
using System.Collections.Generic;
using DotPulsar.Extensions;

namespace Paramore.Brighter.MessagingGateway.Pulsar;

/// <summary>
/// Factory for creating Pulsar message consumers (synchronous and asynchronous) for Brighter's message processing pipeline.
/// </summary>
/// <param name="connection">The connection gateway to Apache Pulsar used for creating consumers.</param>
public class PulsarMessageConsumerFactory(PulsarMessagingGatewayConnection connection) : IAmAMessageConsumerFactory
{
    private static readonly ConcurrentDictionary<PulsarSubscription, PulsarBackgroundMessageConsumer> s_backgroundConsumers = new();
    
    /// <summary>
    /// Creates a consumer for the specified queue.
    /// </summary>
    /// <param name="subscription">The queue to connect to</param>
    /// <returns>IAmAMessageConsumerSync</returns
    public IAmAMessageConsumerSync Create(Subscription subscription) 
        => CreatePulsarConsumer(subscription);

     /// <summary>
     /// Creates a consumer for the specified queue.
     /// </summary>
     /// <param name="subscription">The queue to connect to</param>
     /// <returns>IAmAMessageConsumerSync</returns>
    public IAmAMessageConsumerAsync CreateAsync(Subscription subscription)
        => CreatePulsarConsumer(subscription);

    private PulsarMessageConsumer CreatePulsarConsumer(Subscription subscription)
    {
        if (subscription is not PulsarSubscription pulsarSubscription)
        {
            throw new ConfigurationException("We expect PulsarSubscription or PulsarSubscription<T> as a parameter");
        }
        
        var background = s_backgroundConsumers.GetOrAdd(pulsarSubscription, CreateConsumerBackground);
        background.Start();
        return new PulsarMessageConsumer(background);
    }

    private PulsarBackgroundMessageConsumer CreateConsumerBackground(PulsarSubscription pulsarSubscription)
    {
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

        var maxBufferSize = pulsarSubscription.BufferSize * pulsarSubscription.NoOfPerformers;
        return new PulsarBackgroundMessageConsumer(maxBufferSize, consumer);
    }
}
