using System;
using Amazon.Runtime;
using NUnit.Framework;
using paramore.brighter.commandprocessor.messaginggateway.awssqs;

namespace paramore.brighter.commandprocessor.tests.nunit.MessagingGateway.awssqs
{
    [Category("AWS")]
    [TestFixture]
    public class SqsMessageConsumerReceiveTests
    {
        private TestAWSQueueListener _testQueueListener;
        private IAmAMessageProducer _sender;
        private IAmAMessageConsumer _receiver;
        private Message _sentMessage;
        private Message _receivedMessage;
        private string queueUrl = "https://sqs.eu-west-1.amazonaws.com/027649620536/TestSqsTopicQueue";

        [SetUp]
        public void Establish ()
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
        public void When_reading_a_message_via_the_messaging_gateway()
        {
            _sender.Send(_sentMessage);
            _receivedMessage = _receiver.Receive(2000);
            _receiver.Acknowledge(_receivedMessage);


            //should_send_a_message_via_sqs_with_the_matching_body
            Assert.AreEqual(_sentMessage.Body, _receivedMessage.Body);
            //should_send_a_message_via_sqs_with_the_matching_header_handled_count
            Assert.AreEqual(_sentMessage.Header.HandledCount, _receivedMessage.Header.HandledCount);
            //should_send_a_message_via_sqs_with_the_matching_header_id
            Assert.AreEqual(_sentMessage.Header.Id, _receivedMessage.Header.Id);
            //should_send_a_message_via_sqs_with_the_matching_header_message_type
            Assert.AreEqual(_sentMessage.Header.MessageType, _receivedMessage.Header.MessageType);
            //should_send_a_message_via_sqs_with_the_matching_header_time_stamp
            Assert.AreEqual(_sentMessage.Header.TimeStamp, _receivedMessage.Header.TimeStamp);
            //should_send_a_message_via_sqs_with_the_matching_header_topic
            Assert.AreEqual(_sentMessage.Header.Topic, _receivedMessage.Header.Topic);
            //should_remove_the_message_from_the_queue
            Assert.Null(_testQueueListener.Listen());
        }

        [TearDown]
        public void Cleanup()
        {
            _testQueueListener.DeleteMessage(_receivedMessage.Header.Bag["ReceiptHandle"].ToString());
        }
    }
}