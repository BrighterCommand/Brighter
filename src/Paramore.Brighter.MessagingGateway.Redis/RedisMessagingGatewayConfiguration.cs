using System;
using System.Dynamic;
using StackExchange.Redis;

namespace Paramore.Brighter.MessagingGateway.Redis
{
    public class RedisMessagingGatewayConfiguration
    {
        public string ServerList { get; set; }
        public bool AllowAdmin { get; set; }
        public int ConnectRetry { get; set; }
        public int ConnectTimeout { get; set; }
        public Proxy Proxy { get; set; }
        public int SyncTimeout { get; set; }
        public TimeSpan? MessageTimeToLive { get; set; }
    }
}