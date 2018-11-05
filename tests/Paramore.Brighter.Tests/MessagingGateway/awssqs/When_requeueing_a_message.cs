using System;
using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using FluentAssertions;
using Newtonsoft.Json;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Tests.MessagingGateway.AWSSQS
{
    [Trait("Category", "AWS")]
    public class SqsMessageProducerRequeueTests : IDisposable
    {
        private readonly IAmAMessageProducer _sender;
        private Message _requeuedMessage;
        private Message _receivedMessage;
        private readonly IAmAChannel _channel;
        private readonly InputChannelFactory _channelFactory;
        private readonly Message _message;
        private readonly MyCommand _myCommand;
        private readonly Guid _correlationId;
        private readonly string _replyTo;
        private readonly string _contentType;
        private readonly string _topicName;
 
        public SqsMessageProducerRequeueTests()
        {
            _myCommand = new MyCommand{Value = "Test"};
            _correlationId = Guid.NewGuid();
            _replyTo = "http:\\queueUrl";
            _contentType = "text\\plain";
            _topicName = _myCommand.GetType().FullName.ToString().ToValidSNSTopicName();
            
            _message = new Message(
                new MessageHeader(_myCommand.Id, _topicName, MessageType.MT_COMMAND, _correlationId, _replyTo, _contentType),
                new MessageBody(JsonConvert.SerializeObject(_myCommand))
            );
 
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
            _sender.Send(_message);
            _receivedMessage = _channel.Receive(2000); 
            _channel.Requeue(_receivedMessage);

            _requeuedMessage = _channel.Receive(1000);
            _requeuedMessage.Body.Value.Should().Be(_receivedMessage.Body.Value);
        }

        public void Dispose()
        {
            var connection = new Connection<MyCommand>();
            _channelFactory.DeleteQueue(connection);
            _channelFactory.DeleteTopic(connection);
        }
    }
}
