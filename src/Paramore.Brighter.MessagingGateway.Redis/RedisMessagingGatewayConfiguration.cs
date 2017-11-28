using System;

namespace Paramore.Brighter.MessagingGateway.Redis
{
    public class RedisMessagingGatewayConfiguration
    {
        public int MaxPoolSize { get; set; }
        public TimeSpan? MessageTimeToLive { get; set; }
        public string RedisConnectionString { get; set; }
    }
}