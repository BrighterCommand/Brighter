#region Licence
/* The MIT License (MIT)
Copyright © 2017 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using ServiceStack.Redis;

namespace Paramore.Brighter.MessagingGateway.Redis
{
    public class RedisMessageGateway
    {
        protected TimeSpan MessageTimeToLive;
        protected static Lazy<RedisManagerPool> Pool;
        protected string Topic;
        protected readonly RedisMessagingGatewayConfiguration GatewayConfiguration;

        protected RedisMessageGateway(RedisMessagingGatewayConfiguration redisMessagingGatewayConfiguration)
        {
            GatewayConfiguration = redisMessagingGatewayConfiguration;
            
            Pool = new Lazy<RedisManagerPool>(() =>
            {
                OverrideRedisClientDefaults();

                return new RedisManagerPool(
                    GatewayConfiguration.RedisConnectionString,
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
        protected void OverrideRedisClientDefaults()
        {
            if (GatewayConfiguration.BackoffMultiplier.HasValue)
            {
                RedisConfig.BackOffMultiplier = GatewayConfiguration.BackoffMultiplier.Value;
            }

            if (GatewayConfiguration.BufferLength.HasValue)
            {
                RedisConfig.BufferLength = GatewayConfiguration.BufferLength.Value;
            }
            
            if (GatewayConfiguration.DeactivatedClientsExpiry.HasValue)
            {
                RedisConfig.DeactivatedClientsExpiry = GatewayConfiguration.DeactivatedClientsExpiry.Value;
            }

            if (GatewayConfiguration.DefaultConnectTimeout.HasValue)
            {
                RedisConfig.DefaultConnectTimeout = GatewayConfiguration.DefaultConnectTimeout.Value;
            }

            if (GatewayConfiguration.DefaultIdleTimeOutSecs.HasValue)
            {
                RedisConfig.DefaultIdleTimeOutSecs = GatewayConfiguration.DefaultIdleTimeOutSecs.Value;
            }

            if (GatewayConfiguration.DefaultReceiveTimeout.HasValue)
            {
                RedisConfig.DefaultReceiveTimeout = GatewayConfiguration.DefaultReceiveTimeout.Value;
            }

            if (GatewayConfiguration.DefaultRetryTimeout.HasValue)
            {
                RedisConfig.DefaultRetryTimeout = GatewayConfiguration.DefaultRetryTimeout.Value;
            }

            if (GatewayConfiguration.DefaultSendTimeout.HasValue)
            {
                RedisConfig.DefaultSendTimeout = GatewayConfiguration.DefaultSendTimeout.Value;
            }

            if (GatewayConfiguration.DisableVerboseLogging.HasValue)
            {
                RedisConfig.DisableVerboseLogging = GatewayConfiguration.DisableVerboseLogging.Value;
            }

            if (GatewayConfiguration.HostLookupTimeoutMs.HasValue)
            {
                RedisConfig.HostLookupTimeoutMs = GatewayConfiguration.HostLookupTimeoutMs.Value;
            }

            if (GatewayConfiguration.MaxPoolSize.HasValue)
            {
                RedisConfig.DefaultMaxPoolSize = GatewayConfiguration.MaxPoolSize;
            }

            if (GatewayConfiguration.MessageTimeToLive.HasValue)
            {
                MessageTimeToLive = GatewayConfiguration.MessageTimeToLive.Value;
            }
            else
            {
                MessageTimeToLive = TimeSpan.FromMinutes(10);
            }

            if (GatewayConfiguration.VerifyMasterConnections.HasValue)
            {
                RedisConfig.VerifyMasterConnections = GatewayConfiguration.VerifyMasterConnections.Value;
            }

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
            client.SetValue(key, redisMessage, MessageTimeToLive);
        }

   }
}