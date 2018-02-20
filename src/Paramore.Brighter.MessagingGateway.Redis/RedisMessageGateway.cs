using System;
using ServiceStack.Redis;

namespace Paramore.Brighter.MessagingGateway.Redis
{
    public class RedisMessageGateway
    {
        private readonly TimeSpan _messageTimeToLive;
        protected static Lazy<RedisManagerPool> Pool;
        protected string Topic;
        private readonly RedisMessagingGatewayConfiguration _gatewayConfiguration;

        protected RedisMessageGateway(RedisMessagingGatewayConfiguration redisMessagingGatewayConfiguration)
        {
            _messageTimeToLive = redisMessagingGatewayConfiguration.MessageTimeToLive ?? TimeSpan.FromMinutes(10);
            _gatewayConfiguration = redisMessagingGatewayConfiguration;
            
            Pool = new Lazy<RedisManagerPool>(() =>
            {
                OverrideRedisClientDefaults();

                return new RedisManagerPool(
                    _gatewayConfiguration.RedisConnectionString,
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
        /// Service Stack Redis provides global (static) configuration settings for how Redis behaves.
        /// We want to be able to override the defaults (or leave them if we think they are appropriate
        /// We run this as part of lazy initialization, as these are static and so we want to enforce the idea
        /// that you are globally setting them for any worker that runs in the same process
        /// A client could also set RedisConfig values directly in their own code, if preferred.
        /// Our preference is to hide the global nature of that config inside this Lazy<T>
        /// </summary>
        private void OverrideRedisClientDefaults()
        {
            RedisMessagingGatewayConfiguration redisMessagingGatewayConfiguration;
            if (_gatewayConfiguration.DefaultConnectTimeout.HasValue)
            {
                RedisConfig.DefaultConnectTimeout = _gatewayConfiguration.DefaultConnectTimeout.Value;
            }

            if (_gatewayConfiguration.DefaultSendTimeout.HasValue)
            {
                RedisConfig.DefaultSendTimeout = _gatewayConfiguration.DefaultSendTimeout.Value;
            }

            if (_gatewayConfiguration.DefaultReceiveTimeout.HasValue)
            {
                RedisConfig.DefaultReceiveTimeout = _gatewayConfiguration.DefaultReceiveTimeout.Value;
            }

            if (_gatewayConfiguration.DefaultRetryTimeout.HasValue)
            {
                RedisConfig.DefaultRetryTimeout = _gatewayConfiguration.DefaultRetryTimeout.Value;
            }

            if (_gatewayConfiguration.DefaultIdleTimeOutSecs.HasValue)
            {
                RedisConfig.DefaultIdleTimeOutSecs = _gatewayConfiguration.DefaultIdleTimeOutSecs.Value;
            }

            if (_gatewayConfiguration.BackoffMultiplier.HasValue)
            {
                RedisConfig.BackOffMultiplier = _gatewayConfiguration.BackoffMultiplier.Value;
            }

            if (_gatewayConfiguration.BufferLength.HasValue)
            {
                RedisConfig.BufferLength = _gatewayConfiguration.BufferLength.Value;
            }

            if (_gatewayConfiguration.MaxPoolSize.HasValue)
            {
                RedisConfig.DefaultMaxPoolSize = _gatewayConfiguration.MaxPoolSize;
            }

            if (_gatewayConfiguration.VerifyMasterConnections.HasValue)
            {
                RedisConfig.VerifyMasterConnections = true;
            }

            RedisConfig.HostLookupTimeoutMs = 1000;
            RedisConfig.DeactivatedClientsExpiry = TimeSpan.FromSeconds(15);
            RedisConfig.DisableVerboseLogging = false;
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