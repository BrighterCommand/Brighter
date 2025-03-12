using System;
using Xunit;
using Paramore.Brighter.MessagingGateway.Redis;
using Paramore.Brighter.Redis.Tests.TestDoubles;
using ServiceStack.Redis;

namespace Paramore.Brighter.Redis.Tests.MessagingGateway;

[Collection("Redis Shared Pool")] //shared connection pool so run sequentially
[Trait("Category", "Redis")]
public class RedisGatewayConfigurationTests
{
    [Fact]
    public void When_overriding_client_configuration_via_the_gateway()
    {
        var configuration = new RedisMessagingGatewayConfiguration
        {
            BackoffMultiplier = 5,
            BufferPoolMaxSize = 1024,
            DeactivatedClientsExpiry = TimeSpan.Zero,
            DefaultConnectTimeout = 10,
            DefaultIdleTimeOutSecs = 360,
            DefaultReceiveTimeout = 30,
            DefaultRetryTimeout = 10,
            DefaultSendTimeout = 10,
            DisableVerboseLogging = false,
            HostLookupTimeoutMs = 400,
            MaxPoolSize = 50,
            MessageTimeToLive = TimeSpan.FromMinutes(30),
            VerifyMasterConnections = false
        };

        using var gateway = new TestRedisGateway(configuration, RoutingKey.Empty);
        //Redis Config is static, so we can just look at the values we should have initialized
        Assert.Equal(configuration.BackoffMultiplier.Value, RedisConfig.BackOffMultiplier);
        Assert.Equal(configuration.BackoffMultiplier.Value, RedisConfig.BackOffMultiplier);
        Assert.Equal(configuration.DeactivatedClientsExpiry.Value, RedisConfig.DeactivatedClientsExpiry);
        Assert.Equal(configuration.DefaultConnectTimeout.Value, RedisConfig.DefaultConnectTimeout);
        Assert.Equal(configuration.DefaultIdleTimeOutSecs.Value, RedisConfig.DefaultIdleTimeOutSecs);
        Assert.Equal(configuration.DefaultReceiveTimeout.Value, RedisConfig.DefaultReceiveTimeout);
        Assert.Equal(configuration.DefaultSendTimeout.Value, RedisConfig.DefaultSendTimeout);
        Assert.Equal(!configuration.DisableVerboseLogging.Value, RedisConfig.EnableVerboseLogging);
        Assert.Equal(configuration.HostLookupTimeoutMs.Value, RedisConfig.HostLookupTimeoutMs);
        Assert.Equal(configuration.MaxPoolSize.Value, RedisConfig.DefaultMaxPoolSize);
        Assert.Equal(configuration.MessageTimeToLive.Value, gateway.MessageTimeToLive);
        Assert.Equal(configuration.VerifyMasterConnections.Value, RedisConfig.VerifyMasterConnections);
    }
}
