using System;
using ServiceStack.Redis;

namespace Paramore.Brighter.MessagingGateway.Redis
{
    public class RedisMessageGateway
    {
        private readonly TimeSpan _messageTimeToLive;
        protected static Lazy<RedisManagerPool> Pool;
        protected string Topic;

        protected RedisMessageGateway(RedisMessagingGatewayConfiguration redisMessagingGatewayConfiguration)
        {
            _messageTimeToLive = redisMessagingGatewayConfiguration.MessageTimeToLive ?? TimeSpan.FromMinutes(10);
            
            Pool = new Lazy<RedisManagerPool>(() =>
            {
                RedisConfig.DefaultConnectTimeout = 1 * 1000;
                RedisConfig.DefaultSendTimeout = 1 * 1000;
                RedisConfig.DefaultReceiveTimeout = 1 * 1000;
                if (redisMessagingGatewayConfiguration.DefaultRetryTimeout.HasValue)
                {
                    RedisConfig.DefaultRetryTimeout = redisMessagingGatewayConfiguration.DefaultRetryTimeout.Value;
                }
                RedisConfig.DefaultIdleTimeOutSecs = 240;
                if (redisMessagingGatewayConfiguration.BackoffMultiplier.HasValue)
                {
                    RedisConfig.BackOffMultiplier = redisMessagingGatewayConfiguration.BackoffMultiplier.Value;
                }
                RedisConfig.BufferLength = 1450;
                if (redisMessagingGatewayConfiguration.MaxPoolSize.HasValue)
                {
                    RedisConfig.DefaultMaxPoolSize = redisMessagingGatewayConfiguration.MaxPoolSize;
                }
                RedisConfig.VerifyMasterConnections = true;
                RedisConfig.HostLookupTimeoutMs = 1000;
                RedisConfig.DeactivatedClientsExpiry = TimeSpan.FromSeconds(15);
                RedisConfig.DisableVerboseLogging = false;
                    
                 return new RedisManagerPool(
                    redisMessagingGatewayConfiguration.RedisConnectionString,
                    new RedisPoolConfig()
                );
            });
 
        }

        protected void DisposePool()
        {
            if (Pool.IsValueCreated)
                Pool.Value.Dispose();
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
            var key = Topic + "." + msgId.ToString();
            client.SetValue(key, redisMessage, _messageTimeToLive);
        }

   }
}