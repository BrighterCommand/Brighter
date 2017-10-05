using System;
using FluentAssertions;
using Paramore.Brighter.MessagingGateway.Redis;
using StackExchange.Redis;
using Xunit;

namespace Paramore.Brighter.Tests.MessagingGateway.redis
{
    [Trait("Category", "Redis")]
    public class RedisMessageProducerSendTests : IDisposable
    {
        private const string QueueName = "test";
        private const string Topic = "test";
        private RedisMessageProducer _messageProducer;
        private RedisMessageConsumer _messageConsumer;
        private Message _message;

        public RedisMessageProducerSendTests()
        {
            var configuration = new RedisMessagingGatewayConfiguration
            {
                ServerList = "localhost",
                AllowAdmin = false,
                ConnectRetry = 3,
                ConnectTimeout = 5000,
                Proxy = Proxy.None,
                SyncTimeout = 1000
            };

            var options = ConfigurationOptions.Parse(configuration.ServerList);
            options.AllowAdmin = configuration.AllowAdmin;
            options.ConnectRetry = configuration.ConnectRetry;
            options.ConnectTimeout = configuration.ConnectTimeout;
            options.SyncTimeout = configuration.SyncTimeout;
            options.Proxy = configuration.Proxy;
            var connectionMultiplexer = ConnectionMultiplexer.Connect(options);

            _messageProducer = new RedisMessageProducer(connectionMultiplexer); 
            _messageConsumer = new RedisMessageConsumer(connectionMultiplexer, QueueName, Topic);
            _message = new Message(new MessageHeader(Guid.NewGuid(), Topic, MessageType.MT_COMMAND), new MessageBody("test content"));
        }
        
        
        [Fact]
        public void When_posting_a_message_via_the_messaging_gateway()
        {
            //_messageConsumer.Receive(30000); //Need to receive to subscribe to feed, before we send a message. This returns an empty message we discard
            _messageProducer.Send(_message);
            var sentMessage = _messageConsumer.Receive(30000);
            var messageBody = sentMessage.Body.Value;
            //_messageConsumer.Acknowledge(sentMessage);

            //_should_send_a_message_via_restms_with_the_matching_body
            messageBody.Should().Be(_message.Body.Value);
            
            //_should_have_an_empty_pipe_after_acknowledging_the_message
        }

        public void Dispose()
        {
            //_messageConsumer.Purge();
            _messageProducer.Dispose();
        }
 
    }
}