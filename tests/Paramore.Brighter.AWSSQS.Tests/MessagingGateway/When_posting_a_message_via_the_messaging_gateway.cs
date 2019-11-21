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
    public class SqsMessageProducerSendTests : IDisposable
    {
        private readonly Message _message;
        private readonly IAmAChannel _channel;
        private readonly SqsMessageProducer _messageProducer;
        private readonly ChannelFactory _channelFactory;
        private readonly MyCommand _myCommand;
        private readonly Guid _correlationId;
        private readonly string _replyTo;
        private readonly string _contentType;
        private readonly string _topicName;

        public SqsMessageProducerSendTests()
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
        public void When_posting_a_message_via_the_producer()
        {
            _channel.Purge();
            //arrange
            _messageProducer.Send(_message);
            
            var message =_channel.Receive(2000);
            
            //clear the queue
            _channel.Acknowledge(message);

            //should_send_the_message_to_aws_sqs
            message.Header.MessageType.Should().Be(MessageType.MT_COMMAND);

            message.Id.Should().Be(_myCommand.Id);
            message.Redelivered.Should().BeFalse();
            message.Header.Id.Should().Be(_myCommand.Id);
            message.Header.Topic.Should().Contain(_topicName);
            message.Header.CorrelationId.Should().Be(_correlationId);
            message.Header.ReplyTo.Should().Be(_replyTo);
            message.Header.ContentType.Should().Be(_contentType);
            message.Header.HandledCount.Should().Be(0);
            //allow for clock drift in the following test, more important to have a contemporary timestamp than anything
            message.Header.TimeStamp.Should().BeAfter(RoundToSeconds(DateTime.UtcNow.AddMinutes(-1)));
            message.Header.DelayedMilliseconds.Should().Be(0);
            //{"Id":"cd581ced-c066-4322-aeaf-d40944de8edd","Value":"Test","WasCancelled":false,"TaskCompleted":false}
            message.Body.Value.Should().Be(_message.Body.Value);
        }

        public void Dispose()
        {
            var connection = new Connection<MyCommand>();
            _channelFactory.DeleteQueue(connection);
            _channelFactory.DeleteTopic(connection);
        }
        
        private DateTime RoundToSeconds(DateTime dateTime)
        {
            return new DateTime(dateTime.Ticks - (dateTime.Ticks % TimeSpan.TicksPerSecond), dateTime.Kind);
        }

    }
}
