using System;
using Paramore.Brighter.MessagingGateway.Redis;
using ServiceStack.Redis;

namespace Paramore.Brighter.Tests.MessagingGateway.TestDoubles
{
    public class RedisMessageConsumerTimeoutOnGetClient : RedisMessageConsumer
    {
        private const string PoolTimeoutError =
            "Redis Timeout expired. The timeout period elapsed prior to obtaining a connection from the pool. This may have occurred because all pooled connections were in use.";
        
        public RedisMessageConsumerTimeoutOnGetClient(
            RedisMessagingGatewayConfiguration redisMessagingGatewayConfiguration, 
            string queueName, 
            string topic) 
                : base(redisMessagingGatewayConfiguration, queueName, topic)
        {
        }

        protected override IRedisClient GetClient()
        {
            throw new TimeoutException(PoolTimeoutError);
        }
    }
}