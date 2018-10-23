using System;
using Amazon;
using Amazon.Runtime.CredentialManagement;
using FluentAssertions;
using Newtonsoft.Json;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Tests.MessagingGateway.AWSSQS
{
    [Trait("Category", "AWS")]
    public class SqsMessageProeducerSendTests : IDisposable
    {
        private readonly Message _message;
        private readonly IAmAChannel _channel;
        private readonly SqsMessageProducer _messageProducer;
        private readonly InputChannelFactory _channelFactory;
        private MyCommand _myCommand;
        private Guid _correlationId;
        private string _replyTo;
        private string _contentType;
        private string _topicName;

        public SqsMessageProeducerSendTests()
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

                _channelFactory = new InputChannelFactory(awsConnection, new SqsMessageConsumerFactory(awsConnection));
                _channel = _channelFactory.CreateInputChannel(new Connection<MyCommand>());
                
                _messageProducer = new SqsMessageProducer(awsConnection);
            }
        }


        [Fact]
        public void When_posting_a_message_via_the_producer()
        {
            //arrange
            _messageProducer.Send(_message);
            
            var message =_channel.Receive(2000);

            //should_send_the_message_to_aws_sqs
            message.Id.Should().Be(_myCommand.Id);
            message.Redelivered.Should().BeFalse();
            message.Header.Id.Should().Be(_myCommand.Id);
            message.Header.Topic.Should().Be(_topicName);
            message.Header.CorrelationId.Should().Be(_correlationId);
            message.Header.ReplyTo.Should().Be(_replyTo);
            message.Header.ContentType.Should().Be(_contentType);
            message.Header.MessageType.Should().Be(MessageType.MT_COMMAND);
            message.Header.HandledCount.Should().Be(0);
            message.Header.TimeStamp.Should().BeAfter(DateTime.UtcNow);
            message.Header.DelayedMilliseconds.Should().Be(0);
            message.Body.Should().Be("foo");
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
