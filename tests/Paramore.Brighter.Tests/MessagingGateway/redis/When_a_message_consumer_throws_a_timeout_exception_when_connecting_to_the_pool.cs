using System;
using FluentAssertions;
using Paramore.Brighter.MessagingGateway.Redis;
using Paramore.Brighter.Tests.MessagingGateway.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Tests.MessagingGateway.redis
{
    [Collection("Redis")]
    [Trait("Category", "Redis")]
    public class RedisMessageConsumerOperationInterruptedTests : IDisposable
    {
        private const string QueueName = "test";
        private const string Topic = "test";
        private readonly RedisMessageConsumer _messageConsumer;
        private Exception _exception;

        public RedisMessageConsumerOperationInterruptedTests()
        {
            var configuration = RedisFixture.RedisMessagingGatewayConfiguration();

            _messageConsumer = new RedisMessageConsumerTimeoutOnGetClient(configuration, QueueName, Topic);
        }

        [Fact]
        public void When_a_message_consumer_throws_a_timeout_exception_when_getting_a_client_from_the_pool()
        {
            _exception = Catch.Exception(() => _messageConsumer.Receive(30000)); 
            
            //_should_return_a_channel_failure_exception
            _exception.Should().BeOfType<ChannelFailureException>();
            
            //_should_return_an_explaining_inner_exception
            _exception.InnerException.Should().BeOfType<TimeoutException>();
  
        }
        
        public void Dispose()
        {
            _messageConsumer.Purge();
            _messageConsumer.Dispose();
        }
    }
}
