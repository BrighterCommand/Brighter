using System;
using Amazon.Runtime;
using Amazon.SimpleNotificationService.Model;
using FluentAssertions;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.Tests.MessagingGateway.AWSSQS
{
    [Trait("Category", "AWS")]
    public class SqsMessageProducerMissingTopicTests : IDisposable
    {
        private readonly Message _message;
        private readonly SqsMessageProducer _messageProducer;
        private readonly TestAWSQueueListener _queueListener;
        private Topic _topic;

        public SqsMessageProducerMissingTopicTests()
        {
            _queueListener = new TestAWSQueueListener(new AnonymousAWSCredentials());
            _message = new Message(new MessageHeader(Guid.NewGuid(), "AnotherTestSqsTopic", MessageType.MT_COMMAND), new MessageBody("test content"));

            var credentials = new AnonymousAWSCredentials();
            _messageProducer = new SqsMessageProducer(credentials);
        }

        [Fact]
        public void When_posting_a_message_via_the_messaging_gateway_and_sns_topic_does_not_exist()
        {
            _messageProducer.Send(_message);

            _topic = _queueListener.CheckSnsTopic(_message.Header.Topic);

            //should_create_topic_and_send_the_message
            _topic.Should().NotBeNull();
        }

        public void Dispose()
        {
            _queueListener.DeleteTopic(_message.Header.Topic);
        }
    }
}