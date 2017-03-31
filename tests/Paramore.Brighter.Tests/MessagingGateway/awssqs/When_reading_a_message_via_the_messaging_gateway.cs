using System;
using Amazon.Runtime;
using FluentAssertions;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.Tests.MessagingGateway.AWSSQS
{
    [Trait("Category", "AWS")]
    public class SqsMessageConsumerReceiveTests : IDisposable
    {
        private readonly TestAWSQueueListener _testQueueListener;
        private readonly IAmAMessageProducer _sender;
        private readonly IAmAMessageConsumer _receiver;
        private readonly Message _sentMessage;
        private Message _receivedMessage;
        private readonly string _queueUrl = "https://sqs.eu-west-1.amazonaws.com/027649620536/TestSqsTopicQueue";

        public SqsMessageConsumerReceiveTests()
        {
            var messageHeader = new MessageHeader(Guid.NewGuid(), "TestSqsTopic", MessageType.MT_COMMAND);

            messageHeader.UpdateHandledCount();
            _sentMessage = new Message(messageHeader, new MessageBody("test content"));

            var credentials = new AnonymousAWSCredentials();
            _sender = new SqsMessageProducer(credentials);
            _receiver = new SqsMessageConsumer(credentials, _queueUrl);
            _testQueueListener = new TestAWSQueueListener(credentials, _queueUrl);
        }

        [Fact]
        public void When_reading_a_message_via_the_messaging_gateway()
        {
            _sender.Send(_sentMessage);
            _receivedMessage = _receiver.Receive(2000);
            _receiver.Acknowledge(_receivedMessage);


            //should_send_a_message_via_sqs_with_the_matching_body
            _receivedMessage.Body.Should().Be(_sentMessage.Body);
            //should_send_a_message_via_sqs_with_the_matching_header_handled_count
            _receivedMessage.Header.HandledCount.Should().Be(_sentMessage.Header.HandledCount);
            //should_send_a_message_via_sqs_with_the_matching_header_id
            _receivedMessage.Header.Id.Should().Be(_sentMessage.Header.Id);
            //should_send_a_message_via_sqs_with_the_matching_header_message_type
            _receivedMessage.Header.MessageType.Should().Be(_sentMessage.Header.MessageType);
            //should_send_a_message_via_sqs_with_the_matching_header_time_stamp
            _receivedMessage.Header.TimeStamp.Should().Be(_sentMessage.Header.TimeStamp);
            //should_send_a_message_via_sqs_with_the_matching_header_topic
            _receivedMessage.Header.Topic.Should().Be(_sentMessage.Header.Topic);
            //should_remove_the_message_from_the_queue
            _testQueueListener.Listen().Should().BeNull();
        }

        public void Dispose()
        {
            _testQueueListener.DeleteMessage(_receivedMessage.Header.Bag["ReceiptHandle"].ToString());
        }
    }
}