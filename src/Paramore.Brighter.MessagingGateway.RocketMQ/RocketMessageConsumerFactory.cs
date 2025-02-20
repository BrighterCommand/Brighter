using System.Collections.Generic;
using System.Threading.Tasks;
using Org.Apache.Rocketmq;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessagingGateway.RocketMQ;

/// <summary>
/// The RocketMQ consumer factory
/// </summary>
public class RocketMessageConsumerFactory : IAmAMessageConsumerFactory
{
    private readonly RocketMessagingGatewayConnection _connection;

    /// <summary>
    /// Initialize <see cref="RocketMessagingGatewayConnection"/> 
    /// </summary>
    /// <param name="connection">The connection.</param>
    public RocketMessageConsumerFactory(RocketMessagingGatewayConnection connection)
    {
        _connection = connection;
    }

    /// <inheritdoc />
    public IAmAMessageConsumerSync Create(Subscription subscription)
        => BrighterAsyncContext.Run(async () => await CreateConsumerAsync(subscription));

    /// <inheritdoc />
    public IAmAMessageConsumerAsync CreateAsync(Subscription subscription) 
        => BrighterAsyncContext.Run(async () => await CreateConsumerAsync(subscription));

    internal async Task<RocketMessageConsumer> CreateConsumerAsync(Subscription subscription)
    {
        if (subscription is not RocketSubscription rocketSubscription)
        {
            throw new ConfigurationException("We are expecting a RocketSubscription");
        }

        var builder = new SimpleConsumer.Builder();
        
        builder.SetClientConfig(rocketSubscription.ClientConfig ?? _connection.ClientConfig)
            .SetConsumerGroup(rocketSubscription.ConsumerGroup)
            .SetAwaitDuration(rocketSubscription.ReceiveMessageTimeout)
            .SetSubscriptionExpression(new Dictionary<string, FilterExpression>
            {
                [rocketSubscription.RoutingKey] = rocketSubscription.Filter
            });

        var consumer = await builder.Build();
        return new RocketMessageConsumer(consumer, rocketSubscription.BufferSize,
            rocketSubscription.InvisibilityTimeout);
    }
}
