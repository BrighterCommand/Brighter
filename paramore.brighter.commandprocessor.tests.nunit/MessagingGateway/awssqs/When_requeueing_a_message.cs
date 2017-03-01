using System;
using Amazon.Runtime;
using NUnit.Framework;
using paramore.brighter.commandprocessor.messaginggateway.awssqs;

namespace paramore.brighter.commandprocessor.tests.nunit.MessagingGateway.awssqs
{
    
    [Category("AWS")]
    [TestFixture]
    public class SqsMessageProducerRequeueTests
    {
        private TestAWSQueueListener _testQueueListener;
        private IAmAMessageProducer _sender;
        private IAmAMessageConsumer _receiver;
        private Message _sentMessage;
        private Message _requeuedMessage;
        private Message _receivedMessage;
        private string _receivedReceiptHandle;
        private readonly string _queueUrl = "https://sqs.eu-west-1.amazonaws.com/027649620536/TestSqsTopicQueue";

        [SetUp]
        public void Establish()
        {
            var messageHeader = new MessageHeader(Guid.NewGuid(), "TestSqsTopic", MessageType.MT_COMMAND);

            messageHeader.UpdateHandledCount();
            _sentMessage = new Message(header: messageHeader, body: new MessageBody("test content"));

            var credentials = new AnonymousAWSCredentials();
            _sender = new SqsMessageProducer(credentials);
            _receiver = new SqsMessageConsumer(credentials, _queueUrl);
            _testQueueListener = new TestAWSQueueListener(credentials, _queueUrl);
        }

        [Test]
        public void When_requeueing_a_message()
        {
            _sender.Send(_sentMessage);
            _receivedMessage = _receiver.Receive(2000);
            _receivedReceiptHandle = _receivedMessage.Header.Bag["ReceiptHandle"].ToString();
            _receiver.Requeue(_receivedMessage);

            //should_delete_the_original_message_and_create_new_message
             _requeuedMessage = _receiver.Receive(1000);
            Assert.AreEqual(_receivedMessage.Body.Value, _requeuedMessage.Body.Value);
            Assert.AreNotEqual(_requeuedMessage.Header.Bag["ReceiptHandle"].ToString(), _receivedReceiptHandle);
        }

        [TearDown]
        public void Cleanup()
        {
            _testQueueListener.DeleteMessage(_requeuedMessage.Header.Bag["ReceiptHandle"].ToString());
        }
    }
}