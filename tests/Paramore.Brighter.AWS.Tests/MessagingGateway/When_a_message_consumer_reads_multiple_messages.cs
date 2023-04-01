using System;
using System.Collections.Generic;
using System.Linq;
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
    [Trait("Fragile", "CI")]
    public class SQSBufferedConsumerTests : IDisposable
    {
        private readonly SqsMessageProducer _messageProducer;
        private readonly SqsMessageConsumer _consumer;
        private readonly string _topicName;
        private readonly ChannelFactory _channelFactory;
        private const string _contentType = "text\\plain";
        private const int _bufferSize = 3;
        private const int _messageCount = 4;

        public SQSBufferedConsumerTests()
        {
            (AWSCredentials credentials, RegionEndpoint region) = CredentialsChain.GetAwsCredentials();
            var awsConnection = new AWSMessagingGatewayConnection(credentials, region);

            _channelFactory = new ChannelFactory(awsConnection);
            var channelName = $"Buffered-Consumer-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
            _topicName = $"Buffered-Consumer-Tests-{Guid.NewGuid().ToString()}".Truncate(45);

            //we need the channel to create the queues and notifications
            var routingKey = new RoutingKey(_topicName);

            var channel = _channelFactory.CreateChannel(new SqsSubscription<MyCommand>(
                name: new SubscriptionName(channelName),
                channelName:new ChannelName(channelName),
                routingKey:routingKey,
                bufferSize: _bufferSize,
                makeChannels: OnMissingChannel.Create
                ));

            //we want to access via a consumer, to receive multiple messages - we don't want to expose on channel
            //just for the tests, so create a new consumer from the properties
            _consumer = new SqsMessageConsumer(awsConnection, channel.Name.ToValidSQSQueueName(), routingKey, _bufferSize);
            _messageProducer = new SqsMessageProducer(awsConnection,
                new SnsPublication
                {
                    MakeChannels = OnMissingChannel.Create
                });
        }

        [Fact]
        public void When_a_message_consumer_reads_multiple_messages()
        {
            var messageOne = new Message(
                new MessageHeader(Guid.NewGuid(), _topicName, MessageType.MT_COMMAND, Guid.NewGuid(), string.Empty, _contentType),
                new MessageBody("test content one")
                );

            var messageTwo= new Message(
                new MessageHeader(Guid.NewGuid(), _topicName, MessageType.MT_COMMAND, Guid.NewGuid(), string.Empty, _contentType),
                new MessageBody("test content two")
                );

            var messageThree= new Message(
                new MessageHeader(Guid.NewGuid(), _topicName, MessageType.MT_COMMAND, Guid.NewGuid(), string.Empty, _contentType),
                new MessageBody("test content three")
                );

            var messageFour= new Message(
                new MessageHeader(Guid.NewGuid(), _topicName, MessageType.MT_COMMAND, Guid.NewGuid(), string.Empty, _contentType),
                new MessageBody("test content four")
                );

            //send MESSAGE_COUNT messages
            _messageProducer.Send(messageOne);
            _messageProducer.Send(messageTwo);
            _messageProducer.Send(messageThree);
            _messageProducer.Send(messageFour);


            int iteration = 0;
            var messagesReceived = new List<Message>();
            var messagesReceivedCount = messagesReceived.Count;
            do
            {
                iteration++;
                var outstandingMessageCount = _messageCount - messagesReceivedCount;

                //retrieve  messages
                var messages = _consumer.Receive(10000);

                messages.Length.Should().BeLessOrEqualTo(outstandingMessageCount);

                //should not receive more than buffer in one hit
                messages.Length.Should().BeLessOrEqualTo(_bufferSize);

                var moreMessages = messages.Where(m => m.Header.MessageType == MessageType.MT_COMMAND);
                foreach (var message in moreMessages)
                {
                    messagesReceived.Add(message);
                   _consumer.Acknowledge(message);
                }

                messagesReceivedCount = messagesReceived.Count;

                Task.Delay(1000).Wait();

            } while ((iteration <= 5) && (messagesReceivedCount <  _messageCount));


            messagesReceivedCount.Should().Be(4);
        }

        public void Dispose()
        {
            //Clean up resources that we have created
            _channelFactory.DeleteTopic();
            _channelFactory.DeleteQueue();
        }
    }
}


