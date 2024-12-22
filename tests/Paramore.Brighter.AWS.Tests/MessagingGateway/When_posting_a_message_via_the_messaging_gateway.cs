using System;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
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
        private readonly Message _message;
        private readonly IAmAChannel _channel;
        private readonly SqsMessageProducer _messageProducer;
        private readonly ChannelFactory _channelFactory;
        private readonly MyCommand _myCommand;
        private readonly string _topicName;

        private readonly Message _fifoMessage;
        private readonly IAmAChannel _fifoChannel;
        private readonly SqsMessageProducer _fifoMessageProducer;
        private readonly ChannelFactory _fifoChannelFactory;
        private readonly string _fifoTopicName;
        private readonly string _fifoMessageGroupId;
        private readonly string _fifoDeduplicationId;

        private readonly string _correlationId;
        private readonly string _replyTo;
        private readonly string _contentType;

        public SqsMessageProducerSendTests()
        {
            _myCommand = new MyCommand { Value = "Test" };
            _correlationId = Guid.NewGuid().ToString();
            _replyTo = "http:\\queueUrl";
            _contentType = "text\\plain";
            var channelName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
            _topicName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
            var routingKey = new RoutingKey(_topicName);

            SqsSubscription<MyCommand> subscription = new(
                name: new SubscriptionName(channelName),
                channelName: new ChannelName(channelName),
                routingKey: routingKey,
                rawMessageDelivery: false
            );

            _message = new Message(
                new MessageHeader(_myCommand.Id, routingKey, MessageType.MT_COMMAND, correlationId: _correlationId,
                    replyTo: new RoutingKey(_replyTo), contentType: _contentType),
                new MessageBody(JsonSerializer.Serialize((object)_myCommand, JsonSerialisationOptions.Options))
            );


            (AWSCredentials credentials, RegionEndpoint region) = CredentialsChain.GetAwsCredentials();
            var awsConnection = new AWSMessagingGatewayConnection(credentials, region);

            _channelFactory = new ChannelFactory(awsConnection);
            _channel = _channelFactory.CreateChannel(subscription);

            _messageProducer = new SqsMessageProducer(awsConnection,
                new SnsPublication { Topic = new RoutingKey(_topicName), MakeChannels = OnMissingChannel.Create });

            // FIFO 
            var fifoChannelName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
            _fifoTopicName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
            var fifoRoutingKey = new RoutingKey(_fifoTopicName);

            SqsSubscription<MyCommand> fifoSubscription = new(
                name: new SubscriptionName(fifoChannelName),
                channelName: new ChannelName(channelName),
                routingKey: fifoRoutingKey,
                rawMessageDelivery: false,
                sqsType: SnsSqsType.Fifo
            );
            
            _fifoMessageGroupId = Guid.NewGuid().ToString();
            _fifoDeduplicationId = Guid.NewGuid().ToString();
            _fifoMessage = new Message(
                new MessageHeader(_myCommand.Id, fifoRoutingKey, MessageType.MT_COMMAND, correlationId: _correlationId,
                    replyTo: new RoutingKey(_replyTo), contentType: _contentType,
                    partitionKey: _fifoMessageGroupId)
                {
                    Bag =
                    {
                        [HeaderNames.DeduplicationId] = _fifoDeduplicationId
                    }
                },
                new MessageBody(JsonSerializer.Serialize((object)_myCommand, JsonSerialisationOptions.Options))
            );

            _fifoChannelFactory = new ChannelFactory(awsConnection);
            _fifoChannel = _fifoChannelFactory.CreateChannel(fifoSubscription);

            _fifoMessageProducer = new SqsMessageProducer(awsConnection,
                new SnsPublication
                {
                    Topic = new RoutingKey(_fifoTopicName),
                    MakeChannels = OnMissingChannel.Create,
                    SnsType = SnsSqsType.Fifo,
                    Deduplication = true
                });
        }


        [Theory]
        [InlineData("test subject", true)]
        [InlineData(null, true)]
        [InlineData("test subject", false)]
        [InlineData(null, false)]
        public async Task When_posting_a_message_via_the_producer(string subject, bool sendAsync)
        {
            //arrange
            _message.Header.Subject = subject;
            if (sendAsync)
            {
                await _messageProducer.SendAsync(_message);
            }
            else
            {
                _messageProducer.Send(_message);
            }

            await Task.Delay(1000);

            var message = _channel.Receive(TimeSpan.FromMilliseconds(5000));

            //clear the queue
            _channel.Acknowledge(message);

            //should_send_the_message_to_aws_sqs
            message.Header.MessageType.Should().Be(MessageType.MT_COMMAND);

            message.Id.Should().Be(_myCommand.Id);
            message.Redelivered.Should().BeFalse();
            message.Header.MessageId.Should().Be(_myCommand.Id);
            message.Header.Topic.Value.Should().Contain(_topicName);
            message.Header.CorrelationId.Should().Be(_correlationId);
            message.Header.ReplyTo.Should().Be(_replyTo);
            message.Header.ContentType.Should().Be(_contentType);
            message.Header.HandledCount.Should().Be(0);
            message.Header.Subject.Should().Be(subject);
            //allow for clock drift in the following test, more important to have a contemporary timestamp than anything
            message.Header.TimeStamp.Should().BeAfter(RoundToSeconds(DateTime.UtcNow.AddMinutes(-1)));
            message.Header.Delayed.Should().Be(TimeSpan.Zero);
            //{"Id":"cd581ced-c066-4322-aeaf-d40944de8edd","Value":"Test","WasCancelled":false,"TaskCompleted":false}
            message.Body.Value.Should().Be(_message.Body.Value);
        }

        [Theory]
        [InlineData("test subject", true)]
        [InlineData(null, true)]
        [InlineData("test subject", false)]
        [InlineData(null, false)]
        public async Task When_posting_a_message_via_the_producer_for_fifo(string subject, bool sendAsync)
        {
            //arrange
            _fifoMessage.Header.Subject = subject;
            if (sendAsync)
            {
                await _fifoMessageProducer.SendAsync(_fifoMessage);
            }
            else
            {
                _fifoMessageProducer.Send(_fifoMessage);
            }

            await Task.Delay(1000);

            var message = _fifoChannel.Receive(TimeSpan.FromMilliseconds(5000));

            //clear the queue
            _fifoChannel.Acknowledge(message);

            //should_send_the_message_to_aws_sqs
            message.Header.MessageType.Should().Be(MessageType.MT_COMMAND);

            message.Id.Should().Be(_myCommand.Id);
            message.Redelivered.Should().BeFalse();
            message.Header.MessageId.Should().Be(_myCommand.Id);
            message.Header.Topic.Value.Should().Contain(_fifoTopicName);
            message.Header.CorrelationId.Should().Be(_correlationId);
            message.Header.ReplyTo.Should().Be(_replyTo);
            message.Header.ContentType.Should().Be(_contentType);
            message.Header.HandledCount.Should().Be(0);
            message.Header.Subject.Should().Be(subject);
            //allow for clock drift in the following test, more important to have a contemporary timestamp than anything
            message.Header.TimeStamp.Should().BeAfter(RoundToSeconds(DateTime.UtcNow.AddMinutes(-1)));
            message.Header.Delayed.Should().Be(TimeSpan.Zero);
            //{"Id":"cd581ced-c066-4322-aeaf-d40944de8edd","Value":"Test","WasCancelled":false,"TaskCompleted":false}
            message.Body.Value.Should().Be(_message.Body.Value);
            message.Header.PartitionKey.Should().Be(_fifoMessageGroupId);
            message.Header.Bag.Should().ContainKey(HeaderNames.DeduplicationId);
            message.Header.Bag[HeaderNames.DeduplicationId].Should().Be(_fifoDeduplicationId);
        }


        public void Dispose()
        {
            _channelFactory?.DeleteTopic();
            _channelFactory?.DeleteQueue();
            _messageProducer?.Dispose();

            _fifoChannelFactory?.DeleteTopic();
            _fifoChannelFactory?.DeleteQueue();
            _fifoMessageProducer?.Dispose();
        }

        private static DateTime RoundToSeconds(DateTime dateTime)
        {
            return new DateTime(dateTime.Ticks - (dateTime.Ticks % TimeSpan.TicksPerSecond), dateTime.Kind);
        }
    }
}
