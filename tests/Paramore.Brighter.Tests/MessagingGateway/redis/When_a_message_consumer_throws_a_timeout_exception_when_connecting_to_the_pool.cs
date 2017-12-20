using System;
using Amazon.SimpleNotificationService.Model;
using FluentAssertions;
using Paramore.Brighter.MessagingGateway.Redis;
using Paramore.Brighter.Tests.MessagingGateway.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Tests.MessagingGateway.redis
{
    [Trait("Category", "Redis")]
    public class RmqMessageConsumerOperationInterruptedTests : IDisposable
    {
        private const string QueueName = "test";
        private const string Topic = "test";
        private readonly RedisMessageConsumer _messageConsumer;
        private Exception _exception;

        public RmqMessageConsumerOperationInterruptedTests()
        {
           var configuration = new RedisMessagingGatewayConfiguration
            {
                RedisConnectionString = "localhost:6379?connectTimeout=1&sendTImeout=1000&",
                MaxPoolSize = 10,
                MessageTimeToLive = TimeSpan.FromMinutes(10)
            };

            _messageConsumer = new RedisMessageConsumerTimeoutOnGetClient(configuration, QueueName, Topic);

        }

        [Fact]
        public void When_a_message_consumer_throws_a_timeout_exception_when_getting_a_client_from_the_pool()
        {
            _exception = Catch.Exception(() => _messageConsumer.Receive(30000)); 
            //_should_return_a_channel_failure_exception
            
            _exception.Should().BeOfType<ChannelFailureException>();
            //_should_return_an_explainging_inner_exception
            _exception.InnerException.Should().BeOfType<TimeoutException>();
  
        }
        
        public void Dispose()
        {
            _messageConsumer.Dispose();
        }
    }
}