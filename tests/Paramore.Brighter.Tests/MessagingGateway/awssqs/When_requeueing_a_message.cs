using System;
using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using FluentAssertions;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.Tests.MessagingGateway.AWSSQS
{
    [Trait("Category", "AWS")]
    public class SqsMessageProducerRequeueTests : IDisposable
    {
        private readonly AWSQueueTools _queueTools;
        private readonly IAmAMessageProducer _sender;
        private readonly IAmAMessageConsumer _receiver;
        private readonly Message _sentMessage;
        private Message _requeuedMessage;
        private Message _receivedMessage;
        private string _receivedReceiptHandle;
        private readonly string _queueUrl = "https://sqs.eu-west-1.amazonaws.com/027649620536/TestSqsTopicQueue";

        public SqsMessageProducerRequeueTests()
        {
            var messageHeader = new MessageHeader(Guid.NewGuid(), "TestSqsTopic", MessageType.MT_COMMAND);

            messageHeader.UpdateHandledCount();
            _sentMessage = new Message(messageHeader, new MessageBody("test content"));

            //Must have credentials stored in the SDK Credentials store or shared credentials file
            if (new CredentialProfileStoreChain().TryGetAWSCredentials("default", out var credentials))
            {
                var connection = new AWSMessagingGatewayConnection(credentials, RegionEndpoint.EUWest1);
                _sender = new SqsMessageProducer(connection);
                _receiver = new SqsMessageConsumer(new AWSMessagingGatewayConnection(credentials, RegionEndpoint.EUWest1), _queueUrl);
                _queueTools = new AWSQueueTools(connection, _queueUrl);
            }
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
            _queueTools.DeleteMessage(_requeuedMessage.Header.Bag["ReceiptHandle"].ToString());
        }
    }
}
