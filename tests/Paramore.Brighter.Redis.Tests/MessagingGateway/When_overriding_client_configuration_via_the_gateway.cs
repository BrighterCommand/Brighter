using System;
using Paramore.Brighter.MessagingGateway.Redis;
using Paramore.Brighter.Redis.Tests.TestDoubles;
using ServiceStack.Redis;

namespace Paramore.Brighter.Redis.Tests.MessagingGateway;

[Category("Redis")]
public class RedisGatewayConfigurationTests
{
    [Test]
    public async Task When_overriding_client_configuration_via_the_gateway()
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
        await Assert.That(RedisConfig.BackOffMultiplier).IsEqualTo(configuration.BackoffMultiplier.Value);
        await Assert.That(RedisConfig.BackOffMultiplier).IsEqualTo(configuration.BackoffMultiplier.Value);
        await Assert.That(RedisConfig.DeactivatedClientsExpiry).IsEqualTo(configuration.DeactivatedClientsExpiry.Value);
        await Assert.That(RedisConfig.DefaultConnectTimeout).IsEqualTo(configuration.DefaultConnectTimeout.Value);
        await Assert.That(RedisConfig.DefaultIdleTimeOutSecs).IsEqualTo(configuration.DefaultIdleTimeOutSecs.Value);
        await Assert.That(RedisConfig.DefaultReceiveTimeout).IsEqualTo(configuration.DefaultReceiveTimeout.Value);
        await Assert.That(RedisConfig.DefaultSendTimeout).IsEqualTo(configuration.DefaultSendTimeout.Value);
        await Assert.That(RedisConfig.EnableVerboseLogging).IsEqualTo(!configuration.DisableVerboseLogging.Value);
        await Assert.That(RedisConfig.HostLookupTimeoutMs).IsEqualTo(configuration.HostLookupTimeoutMs.Value);
        await Assert.That(RedisConfig.DefaultMaxPoolSize).IsEqualTo(configuration.MaxPoolSize.Value);
        await Assert.That(gateway.MessageTimeToLive).IsEqualTo(configuration.MessageTimeToLive.Value);
        await Assert.That(RedisConfig.VerifyMasterConnections).IsEqualTo(configuration.VerifyMasterConnections.Value);
    }
}

