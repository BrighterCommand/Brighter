using System;
using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using FluentAssertions;
using Newtonsoft.Json;
using Paramore.Brighter.AWSSQS.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWSSQS.Tests.MessagingGateway
{
    [Collection("AWS")]
    [Trait("Category", "AWS")]
    public class SqsMessageConsumerRequeueTests : IDisposable
    {
        private readonly Message _message;
        private readonly IAmAChannel _channel;
        private readonly SqsMessageProducer _messageProducer;
        private readonly ChannelFactory _channelFactory;
        private readonly MyCommand _myCommand;

        public SqsMessageConsumerRequeueTests()
        {
            _myCommand = new MyCommand{Value = "Test"};
            Guid correlationId = Guid.NewGuid();
            string replyTo = "http:\\queueUrl";
            string contentType = "text\\plain";
            var channelName = $"Consumer-Requeue-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
            string topicName = $"Consumer-Requeue-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
            var routingKey = new RoutingKey(topicName);
            
            SqsConnection<MyCommand> connection = new SqsConnection<MyCommand>(
                name: new ConnectionName(channelName),
                channelName: new ChannelName(channelName),
                routingKey: routingKey
            );
            
            _message = new Message(
                new MessageHeader(_myCommand.Id, topicName, MessageType.MT_COMMAND, correlationId, replyTo, contentType),
                new MessageBody(JsonConvert.SerializeObject((object) _myCommand))
            );
            
            //Must have credentials stored in the SDK Credentials store or shared credentials file
            var credentialChain = new CredentialProfileStoreChain();
            
            (AWSCredentials credentials, RegionEndpoint region) = CredentialsChain.GetAwsCredentials();
            var awsConnection = new AWSMessagingGatewayConnection(credentials, region);
            _channelFactory = new ChannelFactory(awsConnection);
            _channel = _channelFactory.CreateChannel(connection);
            _messageProducer = new SqsMessageProducer(awsConnection, new SqsProducerConnection{MakeChannels = OnMissingChannel.Create, RoutingKey = routingKey});
        }

        [Fact]
        public void When_rejecting_a_message_through_gateway_with_requeue()
        {
            _messageProducer.Send(_message);

            var message = _channel.Receive(1000);
            
            _channel.Reject(message);

            //should requeue_the_message
            message = _channel.Receive(3000);
            
            //clear the queue
            _channel.Acknowledge(message);

            message.Id.Should().Be(_myCommand.Id);
        }

        public void Dispose()
        {
            //Clean up resources that we have created
            _channelFactory.DeleteTopic();
            _channelFactory.DeleteQueue();
        }
    }
}
