using System;
using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using FluentAssertions;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Tests.MessagingGateway.AWSSQS
{
    [Trait("Category", "AWS")]
    public class SqsMessageProducerRequeueTests : IDisposable
    {
        private readonly IAmAMessageProducer _sender;
        private readonly Message _sentMessage;
        private Message _requeuedMessage;
        private Message _receivedMessage;
        private string _receivedReceiptHandle;
        private readonly IAmAChannel _channel;
        private readonly InputChannelFactory _channelFactory;
 
        public SqsMessageProducerRequeueTests()
        {
            var messageHeader = new MessageHeader(Guid.NewGuid(), "TestSqsTopic", MessageType.MT_COMMAND);

            messageHeader.UpdateHandledCount();
            _sentMessage = new Message(messageHeader, new MessageBody("test content"));

            //Must have credentials stored in the SDK Credentials store or shared credentials file
            if (new CredentialProfileStoreChain().TryGetAWSCredentials("default", out var credentials))
            {
                var awsConnection = new AWSMessagingGatewayConnection(credentials, RegionEndpoint.EUWest1);
                _sender = new SqsMessageProducer(awsConnection);
                _channelFactory = new InputChannelFactory(awsConnection, new SqsMessageConsumerFactory(awsConnection));
                _channel = _channelFactory.CreateInputChannel(new Connection<MyCommand>());
            }
        }

        [Fact]
        public void When_requeueing_a_message()
        {
            _sender.Send(_sentMessage);
            _receivedMessage = _channel.Receive(2000); 
            _receivedReceiptHandle = _receivedMessage.Header.Bag["ReceiptHandle"].ToString();
            _channel.Requeue(_receivedMessage);

            //should_delete_the_original_message_and_create_new_message
            _requeuedMessage = _channel.Receive(1000);
            _requeuedMessage.Body.Value.Should().Be(_receivedMessage.Body.Value);
            _requeuedMessage.Header.Bag["ReceiptHandle"].Should().Be(_receivedReceiptHandle);
        }

        public void Dispose()
        {
            var connection = new Connection<MyCommand>();
            _channelFactory.DeleteQueue(connection);
            _channelFactory.DeleteTopic(connection);
        }
    }
}
