using System;
using Amazon.Runtime;
using nUnitShouldAdapter;
using NUnit.Framework;
using paramore.brighter.commandprocessor.messaginggateway.awssqs;

namespace paramore.brighter.commandprocessor.tests.nunit.MessagingGateway.awssqs
{
    [Category("AWS")]
    [TestFixture]
    public class SqsMessageConsumerRequeueTests
    {
        private readonly string _queueUrl = "https://sqs.eu-west-1.amazonaws.com/027649620536/TestSqsTopicQueue";
        private TestAWSQueueListener _testQueueListener;
        private IAmAMessageProducer _sender;
        private IAmAMessageConsumer _receiver;
        private Message _message;
        private Message _listenedMessage;

        [SetUp]
        public void Establish()
        {
            var messageHeader = new MessageHeader(Guid.NewGuid(), "TestSqsTopic", MessageType.MT_COMMAND);

            messageHeader.UpdateHandledCount();
            _message = new Message(header: messageHeader, body: new MessageBody("test content"));

            var credentials = new AnonymousAWSCredentials();
            _sender = new SqsMessageProducer(credentials);
            _receiver = new SqsMessageConsumer(credentials, _queueUrl);
            _testQueueListener = new TestAWSQueueListener(credentials, _queueUrl);

            _sender.Send(_message);

            _listenedMessage = _receiver.Receive(1000);
        }

        [Test]
        public void When_rejecting_a_message_through_gateway_with_requeue()
        {
            _receiver.Reject(_listenedMessage, true);

            //should_requeue_the_message
            var message = _receiver.Receive(1000);
            message.ShouldEqual(_listenedMessage);

        }

        [TearDown]
        public void Cleanup()
        {
            _testQueueListener.DeleteMessage(_listenedMessage.Header.Bag["ReceiptHandle"].ToString());
        }
    }
}