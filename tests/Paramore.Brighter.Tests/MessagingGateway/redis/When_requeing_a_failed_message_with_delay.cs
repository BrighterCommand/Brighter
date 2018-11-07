using System;
using FluentAssertions;
using Paramore.Brighter.MessagingGateway.Redis;
using Xunit;

namespace Paramore.Brighter.Tests.MessagingGateway.redis
{
    [Collection("Redis")]
    [Trait("Category", "Redis")]
    public class RedisRequeueWithDelayTests : IDisposable
    {
        private const string QueueName = "test";
        private const string Topic = "test";
        private readonly RedisMessageProducer _messageProducer;
        private readonly RedisMessageConsumer _messageConsumer;
        private readonly Message _messageOne;

        public RedisRequeueWithDelayTests()
        {
            var configuration = new RedisMessagingGatewayConfiguration
            {
                RedisConnectionString = "redis://localhost:6379?ConnectTimeout=1000&SendTimeout=1000",
                MaxPoolSize = 10,
                MessageTimeToLive = TimeSpan.FromMinutes(10)
            };

            _messageProducer = new RedisMessageProducer(configuration);
            _messageConsumer = new RedisMessageConsumer(configuration, QueueName, Topic);

            _messageOne = new Message(
                new MessageHeader(Guid.NewGuid(), Topic, MessageType.MT_COMMAND),
                new MessageBody("test content")
            );

        }

        [Fact]
        public void When_requeing_a_failed_message_with_delay()
        {
            //clear the queue, and ensure it exists
            _messageConsumer.Receive(1000);
            
            //send & receive a message
            _messageProducer.Send(_messageOne);
            var message = _messageConsumer.Receive(3000);
            message.Header.HandledCount.Should().Be(0);
            message.Header.DelayedMilliseconds.Should().Be(0);
            
            //now requeue with a delay
            _messageConsumer.Requeue(_messageOne, 1000);
            
            //receive and assert
            message = _messageConsumer.Receive(3000);
            message.Header.HandledCount.Should().Be(1);
            message.Header.DelayedMilliseconds.Should().Be(1000);
        }
        
        public void Dispose()
        {
            _messageConsumer.Purge();
            _messageConsumer.Dispose();
            _messageProducer.Dispose();
        }
  
    }
}
