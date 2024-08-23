using System;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using FluentAssertions;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway
{
    [Trait("Category", "AWS")]
    public class SqsMessageProducerSendTests : IDisposable
    {
        private readonly AmazonSQSClient _sqsClient;
        private readonly string _queueName;
        private readonly Message _message;
        private readonly IAmAChannel _channel;
        private readonly SqsMessageProducer _messageProducer;
        private readonly ChannelFactory _channelFactory;
        private readonly MyCommand _myCommand;
        private readonly string _correlationId;
        private readonly string _replyTo;
        private readonly string _contentType;
        private readonly string _topicName;
        private readonly string _subject;

        public SqsMessageProducerSendTests()
        {
            _myCommand = new MyCommand{Value = "Testttttttt"};
            _correlationId = Guid.NewGuid().ToString();
            _replyTo = "http:\\queueUrl";
            _contentType = "text\\plain";
            var channelName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
            _topicName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
            var routingKey = new RoutingKey(_topicName);
            _subject = "test subject";
            
            SqsSubscription<MyCommand> subscription = new(
                name: new SubscriptionName(channelName),
                channelName: new ChannelName(channelName),
                routingKey: routingKey,
                rawMessageDelivery: false
            );
            
            _message = new Message(
                new MessageHeader(_myCommand.Id, _topicName, MessageType.MT_COMMAND, correlationId: _correlationId,
                    replyTo: _replyTo, contentType: _contentType),
                new MessageBody(JsonSerializer.Serialize((object) _myCommand, JsonSerialisationOptions.Options))
            );


            (AWSCredentials credentials, RegionEndpoint region) = CredentialsChain.GetAwsCredentials();
            var awsConnection = new AWSMessagingGatewayConnection(credentials, region);

            _sqsClient = new AmazonSQSClient(credentials, region);
            _queueName = subscription.ChannelName.ToValidSQSQueueName();
            
            _channelFactory = new ChannelFactory(awsConnection);
            _channel = _channelFactory.CreateChannel(subscription);
            
            _messageProducer = new SqsMessageProducer(awsConnection, new SnsPublication
            {
                Topic = new RoutingKey(_topicName),
                MakeChannels = OnMissingChannel.Create,
                SnsSubjectGenerator = _ => _subject
            });
        }



        [Fact]
        public async Task When_posting_a_message_via_the_producer()
        {
            //arrange
            _messageProducer.Send(_message);
            
            await Task.Delay(1000);
            
            var message = _channel.Receive(5000);
            
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
        
        [Fact]
        public async Task When_posting_a_message_via_the_producer_with_subject()
        {
            //arrange
            _messageProducer.Send(_message);
            
            await Task.Delay(1000);

            var message = await ReceiveRaw(5000);
            
            //clear the queue
            await AcknowledgeRaw(message);

            var jsonDocument = JsonDocument.Parse(message.Body);

            jsonDocument.RootElement.TryGetProperty("Subject", out var subject).Should().BeTrue();
            subject.GetString().Should().Be(_subject);
        }

        private async Task<Amazon.SQS.Model.Message> ReceiveRaw(int timeoutMilliseconds)
        {
            var urlResponse = await _sqsClient.GetQueueUrlAsync(_queueName);
            
            var request = new ReceiveMessageRequest(urlResponse.QueueUrl)
            {
                MaxNumberOfMessages = 1,
                WaitTimeSeconds = (int)TimeSpan.FromMilliseconds(timeoutMilliseconds).TotalSeconds,
                MessageAttributeNames = ["All"],
                MessageSystemAttributeNames = ["All"],
            };
            
            var receiveResponse = await _sqsClient.ReceiveMessageAsync(request);
            
            if (receiveResponse.Messages.Count == 0)
            {
                return null;
            }

            return receiveResponse.Messages[0];
        }

        private async Task AcknowledgeRaw(Amazon.SQS.Model.Message message)
        {
            var urlResponse = await _sqsClient.GetQueueUrlAsync(_queueName);
            await _sqsClient.DeleteMessageAsync(new DeleteMessageRequest(urlResponse.QueueUrl, message.ReceiptHandle));
        }

        public void Dispose()
        {
            _channelFactory?.DeleteTopic();
            _channelFactory?.DeleteQueue();
            _messageProducer?.Dispose();
        }
        
        private DateTime RoundToSeconds(DateTime dateTime)
        {
            return new DateTime(dateTime.Ticks - (dateTime.Ticks % TimeSpan.TicksPerSecond), dateTime.Kind);
        }

    }
}
