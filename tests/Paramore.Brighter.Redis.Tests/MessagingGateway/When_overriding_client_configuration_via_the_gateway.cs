using System;
using FluentAssertions;
using Paramore.Brighter.MessagingGateway.Redis;
using ServiceStack.Redis;
using Xunit;

namespace Paramore.Brighter.Redis.Tests.MessagingGateway
{
    [Collection("Redis")]
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
            
            using (var gateway = new TestRedisGateway(configuration))
            {
            //Redis Config is static, so we can just look at the values we should have initialized
            RedisConfig.BackOffMultiplier.Should().Be(configuration.BackoffMultiplier.Value);
            RedisConfig.BackOffMultiplier.Should().Be(configuration.BackoffMultiplier.Value);
            RedisConfig.DeactivatedClientsExpiry.Should().Be(configuration.DeactivatedClientsExpiry.Value);
            RedisConfig.DefaultConnectTimeout.Should().Be(configuration.DefaultConnectTimeout.Value);
            RedisConfig.DefaultIdleTimeOutSecs.Should().Be(configuration.DefaultIdleTimeOutSecs.Value);
            RedisConfig.DefaultReceiveTimeout.Should().Be(configuration.DefaultReceiveTimeout.Value);
            RedisConfig.DefaultSendTimeout.Should().Be(configuration.DefaultSendTimeout.Value);
            RedisConfig.EnableVerboseLogging.Should().Be(!configuration.DisableVerboseLogging.Value);
            RedisConfig.HostLookupTimeoutMs.Should().Be(configuration.HostLookupTimeoutMs.Value);
            RedisConfig.DefaultMaxPoolSize.Should().Be(configuration.MaxPoolSize.Value);
            gateway.MessageTimeToLive.Should().Be(configuration.MessageTimeToLive.Value);
            RedisConfig.VerifyMasterConnections.Should().Be(configuration.VerifyMasterConnections.Value);
            }
        }


        }

    /// <summary>
    /// There are some properties we want to test, use a test wrapper to expose them, instead of leaking from
    /// run-time classes
    /// </summary>
    public class TestRedisGateway : RedisMessageGateway, IDisposable
    {
        public TestRedisGateway(RedisMessagingGatewayConfiguration redisMessagingGatewayConfiguration)
            : base(redisMessagingGatewayConfiguration)
        {
            OverrideRedisClientDefaults();
        }
        

        public new TimeSpan MessageTimeToLive => base.MessageTimeToLive;

        public void Dispose()
        {
            DisposePool();
            Pool = null;
            RedisConfig.Reset();
            GC.SuppressFinalize(this);
        }
    }
}
