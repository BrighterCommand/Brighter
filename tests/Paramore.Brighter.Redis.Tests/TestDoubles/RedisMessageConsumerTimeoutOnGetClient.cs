using System;
using Paramore.Brighter.MessagingGateway.Redis;
using ServiceStack.Redis;

namespace Paramore.Brighter.Redis.Tests.TestDoubles;

public class RedisMessageConsumerTimeoutOnGetClient(
    RedisMessagingGatewayConfiguration redisMessagingGatewayConfiguration,
    ChannelName queueName,
    RoutingKey topic)
    : RedisMessageConsumer(redisMessagingGatewayConfiguration, queueName, topic)
{
    private const string PoolTimeoutError =
        "Redis Timeout expired. The timeout period elapsed prior to obtaining a subscription from the pool. This may have occurred because all pooled connections were in use.";

    protected override IRedisClient GetClient()
    {
        throw new TimeoutException(PoolTimeoutError);
    }
}
