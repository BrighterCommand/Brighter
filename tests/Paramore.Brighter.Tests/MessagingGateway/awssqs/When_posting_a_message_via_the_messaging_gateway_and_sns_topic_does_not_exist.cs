using System;
using Amazon.Runtime;
using Amazon.SimpleNotificationService.Model;
using FluentAssertions;
using Xunit;
using Paramore.Brighter.MessagingGateway.AWSSQS;

namespace Paramore.Brighter.Tests.MessagingGateway.awssqs
{
    [Trait("Category", "AWS")]
    public class SqsMessageProducerMissingTopicTests : IDisposable
    {
        private Message _message;
        private SqsMessageProducer _messageProducer;
        private TestAWSQueueListener _queueListener;
        private Topic _topic;

        public SqsMessageProducerMissingTopicTests()
        {
            _queueListener = new TestAWSQueueListener(new AnonymousAWSCredentials());
            _message = new Message(header: new MessageHeader(Guid.NewGuid(), "AnotherTestSqsTopic", MessageType.MT_COMMAND), body: new MessageBody("test content"));

            var credentials = new AnonymousAWSCredentials();
            _messageProducer = new SqsMessageProducer(credentials);
        }

        [Fact(Skip = "todo: Amazon.Runtime.AmazonClientException : No RegionEndpoint or ServiceURL configured")]
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