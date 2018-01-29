using System;
using FluentAssertions;
using Paramore.Brighter.MessagingGateway.Redis;
using Xunit;

namespace Paramore.Brighter.Tests.MessagingGateway.redis
{
    [Trait("Category", "Redis")]
    public class RedisMessageProducerSendTests : IDisposable
    {
        private const string QueueName = "test";
        private const string Topic = "test";
        private readonly RedisMessageProducer _messageProducer;
        private readonly RedisMessageConsumer _messageConsumer;
        private readonly Message _message;

        public RedisMessageProducerSendTests()
        {
            var configuration = new RedisMessagingGatewayConfiguration
            {
                RedisConnectionString = "localhost:6379?connectTimeout=1&sendTImeout=1000&",
                MaxPoolSize = 10,
                MessageTimeToLive = TimeSpan.FromMinutes(10)
            };

            _messageProducer = new RedisMessageProducer(configuration); 
            _messageConsumer = new RedisMessageConsumer(configuration, QueueName, Topic);
            _message = new Message(
                new MessageHeader(Guid.NewGuid(), Topic, MessageType.MT_COMMAND), 
                new MessageBody("test content")
                );
        }
        
        
        [Fact]
        public void When_posting_a_message_via_the_messaging_gateway()
        {
            _messageConsumer.Receive(30000); //Need to receive to subscribe to feed, before we send a message. This returns an empty message we discard
            _messageProducer.Send(_message);
            var sentMessage = _messageConsumer.Receive(30000);
            var messageBody = sentMessage.Body.Value;
            _messageConsumer.Acknowledge(sentMessage);

            //_should_send_a_message_via_restms_with_the_matching_body
            messageBody.Should().Be(_message.Body.Value);
        }

        public void Dispose()
        {
            _messageConsumer.Purge();
            _messageConsumer.Dispose();
            _messageProducer.Dispose();
        }
 
    }
}