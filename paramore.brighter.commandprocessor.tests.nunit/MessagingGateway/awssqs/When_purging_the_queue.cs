using System;
using Amazon.Runtime;
using NUnit.Framework;
using paramore.brighter.commandprocessor.messaginggateway.awssqs;

namespace paramore.brighter.commandprocessor.tests.nunit.MessagingGateway.awssqs
{
    [Category("AWS")]
    [TestFixture]
    public class SqsMessageConsumerTests
    {
        private TestAWSQueueListener _testQueueListener;
        private IAmAMessageProducer _sender;
        private IAmAMessageConsumer _receiver;
        private Message _sentMessage;
        private string queueUrl = "https://sqs.eu-west-1.amazonaws.com/027649620536/TestSqsTopicQueue";

        [SetUp]
        public void Establish()
        {
            var messageHeader = new MessageHeader(Guid.NewGuid(), "TestSqsTopic", MessageType.MT_COMMAND);

            messageHeader.UpdateHandledCount();
            _sentMessage = new Message(header: messageHeader, body: new MessageBody("test content"));

            var credentials = new AnonymousAWSCredentials();
            _sender = new SqsMessageProducer(credentials);
            _receiver = new SqsMessageConsumer(credentials, queueUrl);
            _testQueueListener = new TestAWSQueueListener(credentials, queueUrl);
        }

        [Test]
        public void When_purging_the_queue()
        {
            _sender.Send(_sentMessage);
            _receiver.Purge();

           //should_clean_the_queue
            _testQueueListener.Listen().ShouldBeNull();
        }
    }
}