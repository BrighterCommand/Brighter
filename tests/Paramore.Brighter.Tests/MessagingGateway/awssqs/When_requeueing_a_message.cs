using System;
using Amazon.Runtime;
using FluentAssertions;
using Xunit;
using Paramore.Brighter.MessagingGateway.AWSSQS;

namespace Paramore.Brighter.Tests.MessagingGateway.awssqs
{
    [Trait("Category", "AWS")]
    public class SqsMessageProducerRequeueTests : IDisposable
    {
        private TestAWSQueueListener _testQueueListener;
        private IAmAMessageProducer _sender;
        private IAmAMessageConsumer _receiver;
        private Message _sentMessage;
        private Message _requeuedMessage;
        private Message _receivedMessage;
        private string _receivedReceiptHandle;
        private readonly string _queueUrl = "https://sqs.eu-west-1.amazonaws.com/027649620536/TestSqsTopicQueue";

        public SqsMessageProducerRequeueTests()
        {
            var messageHeader = new MessageHeader(Guid.NewGuid(), "TestSqsTopic", MessageType.MT_COMMAND);

            messageHeader.UpdateHandledCount();
            _sentMessage = new Message(header: messageHeader, body: new MessageBody("test content"));

            var credentials = new AnonymousAWSCredentials();
            _sender = new SqsMessageProducer(credentials);
            _receiver = new SqsMessageConsumer(credentials, _queueUrl);
            _testQueueListener = new TestAWSQueueListener(credentials, _queueUrl);
        }

        [Fact]
        public void When_requeueing_a_message()
        {
            _sender.Send(_sentMessage);
            _receivedMessage = _receiver.Receive(2000);
            _receivedReceiptHandle = _receivedMessage.Header.Bag["ReceiptHandle"].ToString();
            _receiver.Requeue(_receivedMessage);

            //should_delete_the_original_message_and_create_new_message
             _requeuedMessage = _receiver.Receive(1000);
            _requeuedMessage.Body.Value.Should().Be(_receivedMessage.Body.Value);
            _requeuedMessage.Header.Bag["ReceiptHandle"].Should().Be(_receivedReceiptHandle);
        }

        public void Dispose()
        {
            _testQueueListener.DeleteMessage(_requeuedMessage.Header.Bag["ReceiptHandle"].ToString());
        }
    }
}