using System;
using Amazon;
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
        private MyCommand _myCommand;
        private readonly Guid _correlationId;
        private readonly string _replyTo;
        private readonly string _contentType;
        private readonly string _topicName;

        public SqsMessageConsumerRequeueTests()
        {
            _myCommand = new MyCommand{Value = "Test"};
            _correlationId = Guid.NewGuid();
            _replyTo = "http:\\queueUrl";
            _contentType = "text\\plain";
            _topicName = AWSNameExtensions.ToValidSNSTopicName((string) _myCommand.GetType().FullName.ToString());
            
            _message = new Message(
                new MessageHeader(_myCommand.Id, _topicName, MessageType.MT_COMMAND, _correlationId, _replyTo, _contentType),
                new MessageBody(JsonConvert.SerializeObject((object) _myCommand))
            );
            
            //Must have credentials stored in the SDK Credentials store or shared credentials file
            var credentialChain = new CredentialProfileStoreChain();
            
            if (credentialChain.TryGetAWSCredentials("default", out var credentials) && credentialChain.TryGetProfile("default", out var profile))
            {
                var awsConnection = new AWSMessagingGatewayConnection(credentials, profile.Region);

                _channelFactory = new ChannelFactory(awsConnection, new SqsMessageConsumerFactory(awsConnection));
                _channel = _channelFactory.CreateChannel(new Connection<MyCommand>());
                
                _messageProducer = new SqsMessageProducer(awsConnection);
            }
 
        }

        [Fact]
        public void When_rejecting_a_message_through_gateway_with_requeue()
        {
            _channel.Purge();

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
            var connection = new Connection<MyCommand>();
            _channelFactory.DeleteQueue(connection);
            _channelFactory.DeleteTopic(connection);
        }
    }
}
