using System;
using Amazon.Runtime;
using FluentAssertions;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.Tests.MessagingGateway.AWSSQS
{
    [Trait("Category", "AWS")]
    public class SqsMessageProeducerSendTests : IDisposable
    {
        private readonly string _queueUrl = "https://sqs.eu-west-1.amazonaws.com/027649620536/TestSqsTopicQueue";
        private readonly Message _message;
        private readonly SqsMessageProducer _messageProducer;
        private readonly TestAWSQueueListener _queueListener;
        private Amazon.SQS.Model.Message _listenedMessage;

        public SqsMessageProeducerSendTests()
        {
            _queueListener = new TestAWSQueueListener(new AnonymousAWSCredentials(), _queueUrl);
            _message = new Message(new MessageHeader(Guid.NewGuid(), "TestSqsTopic", MessageType.MT_COMMAND), new MessageBody("test content"));

            var credentials = new AnonymousAWSCredentials();
            _messageProducer = new SqsMessageProducer(credentials);
        }

        [Fact]
        public void When_posting_a_message_via_the_messaging_gateway()
        {
            _messageProducer.Send(_message);
            _listenedMessage = _queueListener.Listen();

            //should_send_the_message_to_aws_sqs
            _listenedMessage.Body.Should().NotBeNull();
        }

        public void Dispose()
        {
            _queueListener.DeleteMessage(_listenedMessage.ReceiptHandle);
        }
    }
}