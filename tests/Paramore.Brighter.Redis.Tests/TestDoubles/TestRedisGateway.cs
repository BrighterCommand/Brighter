using System;
using Paramore.Brighter.MessagingGateway.Redis;
using ServiceStack.Redis;

namespace Paramore.Brighter.Redis.Tests.TestDoubles;

/// <summary>
/// There are some properties we want to test, use a test wrapper to expose them, instead of leaking from
/// run-time classes
/// </summary>
public class TestRedisGateway : RedisMessageGateway, IDisposable
{
    public TestRedisGateway(RedisMessagingGatewayConfiguration redisMessagingGatewayConfiguration, RoutingKey topic)
        : base(redisMessagingGatewayConfiguration, topic)
    {
        OverrideRedisClientDefaults();
    }
        

    public new TimeSpan MessageTimeToLive => base.MessageTimeToLive;

    public void Dispose()
    {
        DisposePool();
        RedisConfig.Reset();
    }
}
