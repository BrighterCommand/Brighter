using System;
using FluentAssertions;
using Paramore.Brighter.MessagingGateway.Redis;
using Paramore.Brighter.Redis.Tests.Fixtures;
using Paramore.Brighter.Redis.Tests.TestDoubles;
using ServiceStack.Redis;
using Xunit;

namespace Paramore.Brighter.Redis.Tests.MessagingGateway
{
    [Collection("Redis")]
    [Trait("Category", "Redis")]
    public class RedisMessageConsumerRedisNotAvailableTests : IDisposable
    {
        private const string QueueName = "test";
        private const string Topic = "test";
        private readonly RedisMessageConsumer _messageConsumer;
        private Exception _exception;

        public RedisMessageConsumerRedisNotAvailableTests()
        {
            var configuration = RedisFixture.RedisMessagingGatewayConfiguration();

            _messageConsumer = new RedisMessageConsumerSocketErrorOnGetClient(configuration, QueueName, Topic);

        }

        [Fact]
        public void When_a_message_consumer_throws_a_socket_exception_when_connecting_to_the_server()
        {
            _exception = Catch.Exception(() => _messageConsumer.Receive(1000)); 
            
            //_should_return_a_channel_failure_exception
            _exception.Should().BeOfType<ChannelFailureException>();
            
            //_should_return_an_explainging_inner_exception
            _exception.InnerException.Should().BeOfType<RedisException>();
  
        }
        
        public void Dispose()
        {
            _messageConsumer.Purge();
            _messageConsumer.Dispose();
        }
    }
}
