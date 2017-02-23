using System;
using Amazon.Runtime;
using Amazon.SimpleNotificationService.Model;
using nUnitShouldAdapter;
using NUnit.Framework;
using paramore.brighter.commandprocessor.messaginggateway.awssqs;

namespace paramore.brighter.commandprocessor.tests.nunit.MessagingGateway.awssqs
{
    [Category("AWS")]
    [TestFixture]
    public class SqsMessageProducerMissingTopicTests
    {
        private Message _message;
        private SqsMessageProducer _messageProducer;
        private TestAWSQueueListener _queueListener;
        private Topic _topic;

        [SetUp]
        public void Establish()
        {
            _queueListener = new TestAWSQueueListener(new AnonymousAWSCredentials());
            _message = new Message(header: new MessageHeader(Guid.NewGuid(), "AnotherTestSqsTopic", MessageType.MT_COMMAND), body: new MessageBody("test content"));

            var credentials = new AnonymousAWSCredentials();
            _messageProducer = new SqsMessageProducer(credentials);
        }

        [Test]
        public void When_posting_a_message_via_the_messaging_gateway_and_sns_topic_does_not_exist()
        {
            _messageProducer.Send(_message);

            _topic = _queueListener.CheckSnsTopic(_message.Header.Topic);

            //should_create_topic_and_send_the_message
            _topic.ShouldNotBeNull();
        }

        [TearDown]
        public void Cleanup()
        {
            _queueListener.DeleteTopic(_message.Header.Topic);
        }
    }
}