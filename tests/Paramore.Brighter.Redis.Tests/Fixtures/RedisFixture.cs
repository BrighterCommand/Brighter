using System;
using Paramore.Brighter.MessagingGateway.Redis;

namespace Paramore.Brighter.Redis.Tests.Fixtures
{
    public class RedisFixture : IDisposable
    {
        private const string QueueName = "test";
        protected const string Topic = "test";
        public readonly RedisMessageProducer Producer;
        public readonly RedisMessageConsumer Consumer;

        public RedisFixture()
        {
            RedisMessagingGatewayConfiguration configuration = RedisMessagingGatewayConfiguration();

            Producer = new RedisMessageProducer(configuration);
            Consumer = new RedisMessageConsumer(configuration, QueueName, Topic);
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
            Consumer.Purge();
            Consumer.Dispose();
            Producer.Dispose();
        }
    }
}
