using System;
using Amazon.Runtime;
using NUnit.Framework;
using Paramore.Brighter.MessagingGateway.AWSSQS;

namespace paramore.brighter.commandprocessor.tests.nunit.MessagingGateway.awssqs
{

    [Category("AWS")]
    [TestFixture]
    public class SqsMessageProeducerSendTests
    {
        private string queueUrl = "https://sqs.eu-west-1.amazonaws.com/027649620536/TestSqsTopicQueue";
        private Message _message;
        private SqsMessageProducer _messageProducer;
        private TestAWSQueueListener _queueListener;
        private Amazon.SQS.Model.Message _listenedMessage;

        [SetUp]
        public void Establish()
        {
            _queueListener = new TestAWSQueueListener(new AnonymousAWSCredentials(), queueUrl);
            _message = new Message(header: new MessageHeader(Guid.NewGuid(), "TestSqsTopic", MessageType.MT_COMMAND), body: new MessageBody("test content"));

            var credentials = new AnonymousAWSCredentials();
            _messageProducer = new SqsMessageProducer(credentials);
        }

        [Test]
        public void When_posting_a_message_via_the_messaging_gateway()
        {
            _messageProducer.Send(_message);
            _listenedMessage = _queueListener.Listen();

            //should_send_the_message_to_aws_sqs
            Assert.NotNull(_listenedMessage.Body);
        }

        [TearDown]
        public void Cleanup()
        {
            _queueListener.DeleteMessage(_listenedMessage.ReceiptHandle);
        }
    }
}