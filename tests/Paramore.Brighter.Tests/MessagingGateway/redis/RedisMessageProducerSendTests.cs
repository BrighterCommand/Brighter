using System;
using Xunit;

namespace Paramore.Brighter.Tests.MessagingGateway.redis
{
    [Trait("Category", "Redis")]
    public class RedisMessageProducerSendTests : IDisposable
    {

        public RedisMessageProducerSendTests()
        {
            var configuration = new RedisMessagingGatewayConfiguration
            {
                ServerList = "localhost",
                AllowAdmin = false,
                ConnectRetry = 3,
                ConnectTimeout = 5000,
                Password = null,
                Proxy = null,
                SyncTimeout = 1000
            };
            
           _messageProducer = new RedisMessageProducer(); 
        }
        
        
        public void Dispose()
        {
        }

    }
}