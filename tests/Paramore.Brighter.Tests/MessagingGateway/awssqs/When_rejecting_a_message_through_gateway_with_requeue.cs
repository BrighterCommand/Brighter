using System;
using Amazon.Runtime;
using FluentAssertions;
using Xunit;
using Paramore.Brighter.MessagingGateway.AWSSQS;

namespace Paramore.Brighter.Tests.MessagingGateway.awssqs
{
    [Trait("Category", "AWS")]
    public class SqsMessageConsumerRequeueTests : IDisposable
    {
        private readonly string _queueUrl = "https://sqs.eu-west-1.amazonaws.com/027649620536/TestSqsTopicQueue";
        private TestAWSQueueListener _testQueueListener;
        private IAmAMessageProducer _sender;
        private IAmAMessageConsumer _receiver;
        private Message _message;
        private Message _listenedMessage;

        public SqsMessageConsumerRequeueTests()
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

        [Fact]
        public void When_rejecting_a_message_through_gateway_with_requeue()
        {
            _receiver.Reject(_listenedMessage, true);

            //should_requeue_the_message
            var message = _receiver.Receive(1000);
            message.Should().Be(_listenedMessage);
        }

        public void Dispose()
        {
            _testQueueListener.DeleteMessage(_listenedMessage.Header.Bag["ReceiptHandle"].ToString());
        }
    }
}