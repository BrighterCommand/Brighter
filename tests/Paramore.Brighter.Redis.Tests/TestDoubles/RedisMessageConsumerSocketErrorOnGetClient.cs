using System.Net.Sockets;
using Paramore.Brighter.MessagingGateway.Redis;
using ServiceStack.Redis;

namespace Paramore.Brighter.Redis.Tests.TestDoubles
{
    public class RedisMessageConsumerSocketErrorOnGetClient : RedisMessageConsumer
    {
        private const string SocketException =
            "localhost:6379";
        
        public RedisMessageConsumerSocketErrorOnGetClient(
            RedisMessagingGatewayConfiguration redisMessagingGatewayConfiguration, 
            string queueName, 
            string topic) 
                : base(redisMessagingGatewayConfiguration, queueName, topic)
        {
        }

        protected override IRedisClient GetClient()
        {
            throw new RedisException(SocketException, new SocketException((int) SocketError.AccessDenied));
        }

    }
}