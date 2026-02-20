using System.Collections.Generic;
using System.Threading.Tasks;
using Org.Apache.Rocketmq;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.RocketMQ;

/// <summary>
/// RocketMQ message producer implementation for Brighter.
/// Integrates RocketMQ's producer group pattern and transactional message support.
/// </summary>
public class RocketMessageConsumerFactory(RocketMessagingGatewayConnection connection) : IAmAMessageConsumerFactory
{
    /// <inheritdoc />
    public IAmAMessageConsumerSync Create(Subscription subscription)
        => BrighterAsyncContext.Run(() => CreateConsumerAsync(subscription));

    /// <inheritdoc />
    public IAmAMessageConsumerAsync CreateAsync(Subscription subscription) 
        => BrighterAsyncContext.Run(() => CreateConsumerAsync(subscription));

    internal async Task<RocketMessageConsumer> CreateConsumerAsync(Subscription subscription)
    {
        if (subscription is not RocketSubscription rocketSubscription)
        {
            throw new ConfigurationException("We are expecting a RocketSubscription");
        }

        var builder = new SimpleConsumer.Builder();
        
        builder.SetClientConfig(connection.ClientConfig)
            .SetConsumerGroup(rocketSubscription.ConsumerGroup)
            .SetAwaitDuration(rocketSubscription.ReceiveMessageTimeout)
            .SetSubscriptionExpression(new Dictionary<string, FilterExpression>
            {
                [rocketSubscription.RoutingKey] = rocketSubscription.Filter
            });

        var deadLetterRoutingKey = (subscription as IUseBrighterDeadLetterSupport)?.DeadLetterRoutingKey;
        var invalidMessageRoutingKey = (subscription as IUseBrighterInvalidMessageSupport)?.InvalidMessageRoutingKey;

        var consumer = await builder.Build();
        return new RocketMessageConsumer(consumer, rocketSubscription.BufferSize,
            rocketSubscription.InvisibilityTimeout, connection, deadLetterRoutingKey, invalidMessageRoutingKey);
    }
}
