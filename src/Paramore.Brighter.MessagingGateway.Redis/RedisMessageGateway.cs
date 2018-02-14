using System;
using ServiceStack;
using ServiceStack.Redis;

namespace Paramore.Brighter.MessagingGateway.Redis
{
    public class RedisMessageGateway
    {
        private TimeSpan _messageTimeToLive;
        protected static Lazy<RedisManagerPool> _pool;
        protected string _topic;

        protected RedisMessageGateway(RedisMessagingGatewayConfiguration redisMessagingGatewayConfiguration)
        {
            _messageTimeToLive = redisMessagingGatewayConfiguration.MessageTimeToLive ?? TimeSpan.FromMinutes(10);
            _pool = new Lazy<RedisManagerPool>(() => new RedisManagerPool(
                redisMessagingGatewayConfiguration.RedisConnectionString, 
                new RedisPoolConfig() {MaxPoolSize = redisMessagingGatewayConfiguration.MaxPoolSize}
            ));
 
        }

        protected void DisposePool()
        {
            if (_pool.IsValueCreated)
                _pool.Value.Dispose();
        }

        /// <summary>
        /// Creates a plain/text JSON representation of the message
        /// </summary>
        /// <param name="message">The Brighter message to convert</param>
        /// <returns></returns>
        protected static string CreateRedisMessage(Message message)
        {
            //Convert the message into something we can put out via Redis i.e. a string
            var redisMessage = RedisMessagePublisher.EMPTY_MESSAGE;
            using (var redisMessageFactory = new RedisMessagePublisher())
            {
                redisMessage = redisMessageFactory.Create(message);
            }
            return redisMessage;
        }
        
        /// <summary>
        /// Store the actual message content to Redis - we only want one copy, regardless of number of queues
        /// </summary>
        /// <param name="client">The connection to Redis</param>
        /// <param name="redisMessage">The message to write to Redis</param>
        /// <param name="msgId">The id to store it under</param>
        protected void StoreMessage(IRedisClient client, string redisMessage, long msgId)
        {
            //we store the message at topic + msg id
            var key = _topic + "." + msgId.ToString();
            client.SetValue(key, redisMessage, _messageTimeToLive);
        }

   }
}