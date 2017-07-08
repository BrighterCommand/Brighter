using System;
using System.Threading.Tasks;
using Amazon.Runtime;
using FluentAssertions;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.Tests.MessagingGateway.AWSSQS
{
    [Trait("Category", "AWS")]
    public class SqsMessageConsumerTests
    {
        private readonly TestAWSQueueListener _testQueueListener;
        private readonly IAmAMessageProducer _sender;
        private readonly IAmAMessageConsumer _receiver;
        private readonly Message _sentMessage;
        private readonly string _queueUrl = "https://sqs.eu-west-1.amazonaws.com/027649620536/TestSqsTopicQueue";

        public SqsMessageConsumerTests()
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
        public async Task When_purging_the_queue()
        {
            _sender.Send(_sentMessage);
            await _receiver.PurgeAsync();

           //should_clean_the_queue
            _testQueueListener.Listen().Should().BeNull();
        }
    }
}