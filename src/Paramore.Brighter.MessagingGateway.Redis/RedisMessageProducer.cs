using System;
using StackExchange.Redis;

namespace Paramore.Brighter.MessagingGateway.Redis
{
    public class RedisMessageProducer : IAmAMessageProducer
    {
        private static volatile ConnectionMultiplexer _redis;
        private static readonly object SyncRoot = new object();

        public RedisMessageProducer(RedisMessagingGatewayConfiguration configuration)
        {
            //We don't want to recreate this each time, so use a static instance
            if (_redis == null)
            {
                lock (SyncRoot)
                {
                    if (_redis  == null)
                    {
                        var options = ConfigurationOptions.Parse(configuration.ServerList);
                        options.AllowAdmin = configuration.AllowAdmin;
                        options.ConnectRetry = configuration.ConnectRetry;
                        options.ConnectTimeout = configuration.ConnectTimeout;
                        options.SyncTimeout = configuration.SyncTimeout;
                        options.Proxy = configuration.Proxy;
                        _redis = ConnectionMultiplexer.Connect(options);
                    }
                }

            }
                
            
        }

        public void Dispose()
        {
        }

        public void Send(Message message)
        {
            var sub =_redis.GetSubscriber();
            sub.Publish(message.Header.Topic, BrighterRedisMessage.Write(message));
        }
    }
}