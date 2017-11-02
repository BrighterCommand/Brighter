using System;
using FluentAssertions;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;

namespace Paramore.Brighter.Tests.MessagingGateway.Kafka
{
    [Trait("Category", "KAFKA")]
    public class KafkaMessageProducerSendTests : IDisposable
    {
        private const string QueueName = "test";
        private const string Topic = "test";
        private IAmAMessageProducer _messageProducer;
        private IAmAMessageConsumer _messageConsumer;
        private Message _message;

        public KafkaMessageProducerSendTests()
        {
            var gatewayConfiguration = new KafkaMessagingGatewayConfiguration
            {
                Name = "paramore.brighter",
                BootStrapServers = new[] { "localhost:9092" }
            };

            _messageProducer = new KafkaMessageProducerFactory(gatewayConfiguration).Create();
            _messageConsumer = new KafkaMessageConsumerFactory(gatewayConfiguration).Create(QueueName, Topic, false, 10, false);
            var messageGuid = Guid.NewGuid();
            var messageBodyData = "test content {" + messageGuid + "}";
            _message = new Message(new MessageHeader(messageGuid, Topic, MessageType.MT_COMMAND), new MessageBody(messageBodyData));
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
            //_should_have_an_empty_pipe_after_acknowledging_the_message
        }

        public void Dispose()
        {
            _messageConsumer.Dispose();
            _messageProducer.Dispose();
        }
    }
}