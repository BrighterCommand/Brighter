using System;
using FluentAssertions;
using Paramore.Brighter.MessagingGateway.Redis;
using Xunit;

namespace Paramore.Brighter.Tests.MessagingGateway.redis
{
    [Trait("Category", "Redis")]
    [Collection("Redis")]
    public class RedisRequeueMessageTests : IDisposable
    {
        private const string QueueName = "test";
        private const string Topic = "test";
        private readonly RedisMessageProducer _messageProducer;
        private readonly RedisMessageConsumer _messageConsumer;
        private readonly Message _messageOne;
        private readonly Message _messageTwo;


        public RedisRequeueMessageTests()
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

            _messageTwo = new Message(
                new MessageHeader(Guid.NewGuid(), Topic, MessageType.MT_COMMAND),
                new MessageBody("more test content")
            );

        }


        [Fact]
        public void When_requeing_a_failed_message()
        {
            //Need to receive to subscribe to feed, before we send a message. This returns an empty message we discard
            _messageConsumer.Receive(1000);

            //Send a sequence of messages, we want to check that ordering is preserved
            _messageProducer.Send(_messageOne);
            _messageProducer.Send(_messageTwo);

            //Now receive, the first message 
            var sentMessageOne = _messageConsumer.Receive(30000);

            //now requeue the first message
            _messageConsumer.Requeue(_messageOne, 300);

            //try receiving again; messageTwo should come first
            var sentMessageTwo = _messageConsumer.Receive(30000);
            var messageBodyTwo = sentMessageTwo.Body.Value;
            _messageConsumer.Acknowledge(sentMessageTwo);
            
            sentMessageOne = _messageConsumer.Receive(3000);
            var messageBodyOne = sentMessageOne.Body.Value;
            _messageConsumer.Acknowledge(sentMessageOne);


            //_should_send_a_message_via_restms_with_the_matching_body
            messageBodyOne.Should().Be(_messageOne.Body.Value);
            messageBodyTwo.Should().Be(_messageTwo.Body.Value);
        }

        public void Dispose()
        {
            _messageConsumer.Purge();
            _messageConsumer.Dispose();
            _messageProducer.Dispose();
        }
    }
}
