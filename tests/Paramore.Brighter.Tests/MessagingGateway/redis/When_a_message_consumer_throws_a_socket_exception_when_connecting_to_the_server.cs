using System;
using Amazon.SimpleNotificationService.Model;
using FluentAssertions;
using Paramore.Brighter.MessagingGateway.Redis;
using Paramore.Brighter.Tests.MessagingGateway.TestDoubles;
using ServiceStack.Redis;
using Xunit;

namespace Paramore.Brighter.Tests.MessagingGateway.redis
{
    [Trait("Category", "Redis")]
    [Collection("Redis")]
    public class RmqMessageConsumerRedisNotAvailableTests : IDisposable
    {
        private const string QueueName = "test";
        private const string Topic = "test";
        private readonly RedisMessageConsumer _messageConsumer;
        private Exception _exception;

        public RmqMessageConsumerRedisNotAvailableTests()
        {
           var configuration = new RedisMessagingGatewayConfiguration
            {
                RedisConnectionString = "localhost:6379?connectTimeout=1&sendTImeout=1000&",
                MaxPoolSize = 10,
                MessageTimeToLive = TimeSpan.FromMinutes(10)
            };

            _messageConsumer = new RedisMessageConsumerSocketErrorOnGetClient(configuration, QueueName, Topic);

        }

        [Fact]
        public void When_a_message_consumer_throws_a_socket_exception_when_connecting_to_the_server()
        {
            _exception = Catch.Exception(() => _messageConsumer.Receive(30000)); 
            
            //_should_return_a_channel_failure_exception
            _exception.Should().BeOfType<ChannelFailureException>();
            
            //_should_return_an_explainging_inner_exception
            _exception.InnerException.Should().BeOfType<RedisException>();
  
        }
        
        public void Dispose()
        {
            _messageConsumer.Dispose();
        }
    }
}
