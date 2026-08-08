using System;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.Redis;

namespace Paramore.Brighter.Redis.Tests.MessagingGateway
{
    public class RedisFixture : IAsyncDisposable, IDisposable
    {
        public readonly RoutingKey Topic;
        public readonly RedisMessageProducer MessageProducer;
        public readonly RedisMessageConsumer MessageConsumer;

        public RedisFixture()
        {
            var uniqueId = Guid.NewGuid().ToString("N")[..8];
            Topic = new RoutingKey($"test-{uniqueId}");
            var queueName = new ChannelName($"test-{uniqueId}");

            RedisMessagingGatewayConfiguration configuration = RedisMessagingGatewayConfiguration();

            MessageProducer = new RedisMessageProducer(configuration, new RedisMessagePublication {Topic = Topic}, loggerFactory: global::Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
            MessageConsumer = new RedisMessageConsumer(configuration, queueName, Topic, loggerFactory: global::Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        }

        public static RedisMessagingGatewayConfiguration RedisMessagingGatewayConfiguration()
        {
            var configuration = new RedisMessagingGatewayConfiguration
            {
                RedisConnectionString = "redis://localhost:6379?ConnectTimeout=1000&SendTimeout=1000",
                MaxPoolSize = 10,
                MessageTimeToLive = TimeSpan.FromMinutes(10),
                DefaultRetryTimeout = 3000
            };
            return configuration;
        }

        public void Dispose()
        {
            MessageConsumer.Purge();
            MessageConsumer.Dispose();
            MessageProducer.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            await MessageConsumer.PurgeAsync();
            await MessageConsumer.DisposeAsync();
            await MessageProducer.DisposeAsync();
        }
    }
}
