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


        private readonly SqsMessageProducer _fifoMessageProducer;
        private readonly SqsMessageConsumer _fifoConsumer;
        private readonly string _fifoTopicName;
        private readonly ChannelFactory _fifoChannelFactory;

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
                channelName: new ChannelName(channelName),
                routingKey: routingKey,
                bufferSize: _bufferSize,
                makeChannels: OnMissingChannel.Create
            ));

            //we want to access via a consumer, to receive multiple messages - we don't want to expose on channel
            //just for the tests, so create a new consumer from the properties
            _consumer = new SqsMessageConsumer(awsConnection, channel.Name.ToValidSQSQueueName(), routingKey,
                _bufferSize);
            _messageProducer = new SqsMessageProducer(awsConnection,
                new SnsPublication { MakeChannels = OnMissingChannel.Create });

            // Because fifo can modify the topic name we need to run the same test twice, one for standard queue and another to FIFO
            _fifoChannelFactory = new ChannelFactory(awsConnection);
            var fifoChannelName = $"Buffered-Consumer-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
            _fifoTopicName = $"Buffered-Consumer-Tests-{Guid.NewGuid().ToString()}".Truncate(45);

            //we need the channel to create the queues and notifications
            var fifoRoutingKey = new RoutingKey(_fifoTopicName);

            var fifoChannel = _fifoChannelFactory.CreateChannel(new SqsSubscription<MyCommand>(
                name: new SubscriptionName(fifoChannelName),
                channelName: new ChannelName(fifoChannelName),
                routingKey: fifoRoutingKey,
                bufferSize: _bufferSize,
                makeChannels: OnMissingChannel.Create,
                sqsType: SnsSqsType.Fifo
            ));

            //we want to access via a consumer, to receive multiple messages - we don't want to expose on channel
            //just for the tests, so create a new consumer from the properties
            _fifoConsumer = new SqsMessageConsumer(awsConnection, fifoChannel.Name.ToValidSQSQueueName(), fifoRoutingKey,
                _bufferSize);
            _fifoMessageProducer = new SqsMessageProducer(awsConnection,
                new SnsPublication
                {
                    MakeChannels = OnMissingChannel.Create,
                    SnsType = SnsSqsType.Fifo,
                    Deduplication = true
                });
        }

        [Fact]
        public async Task When_a_message_consumer_reads_multiple_messages()
        {
            var routingKey = new RoutingKey(_topicName);

            var messageOne = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND,
                    correlationId: Guid.NewGuid().ToString(), contentType: _contentType),
                new MessageBody("test content one")
            );

            var messageTwo = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND,
                    correlationId: Guid.NewGuid().ToString(), contentType: _contentType),
                new MessageBody("test content two")
            );

            var messageThree = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND,
                    correlationId: Guid.NewGuid().ToString(), contentType: _contentType),
                new MessageBody("test content three")
            );

            var messageFour = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND,
                    correlationId: Guid.NewGuid().ToString(), contentType: _contentType),
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
                var messages = _consumer.Receive(TimeSpan.FromMilliseconds(10000));

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

                await Task.Delay(1000);
            } while ((iteration <= 5) && (messagesReceivedCount < _messageCount));


            messagesReceivedCount.Should().Be(4);
        }

        [Fact]
        public async Task When_a_message_consumer_reads_multiple_messages_per_group()
        {
            var routingKey = new RoutingKey(_fifoTopicName);
            var messageGroupIdOne = "123";

            var messageOne = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND,
                    correlationId: Guid.NewGuid().ToString(), contentType: _contentType,
                    partitionKey: messageGroupIdOne),
                new MessageBody("test content one")
            );

            var messageTwo = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND,
                    correlationId: Guid.NewGuid().ToString(), contentType: _contentType,
                    partitionKey: messageGroupIdOne),
                new MessageBody("test content two")
            );

            var messageThree = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND,
                    correlationId: Guid.NewGuid().ToString(), contentType: _contentType,
                    partitionKey: messageGroupIdOne),
                new MessageBody("test content three")
            );

            var messageFour = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND,
                    correlationId: Guid.NewGuid().ToString(), contentType: _contentType,
                    partitionKey: messageGroupIdOne),
                new MessageBody("test content four")
            );
            var messageGroupIdTwo = "1234";
            var messageFive = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND,
                    correlationId: Guid.NewGuid().ToString(), contentType: _contentType,
                    partitionKey: messageGroupIdTwo),
                new MessageBody("test content five")
            );

            var messageSix = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND,
                    correlationId: Guid.NewGuid().ToString(), contentType: _contentType,
                    partitionKey: messageGroupIdTwo),
                new MessageBody("test content six")
            );

            var messageSeven = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND,
                    correlationId: Guid.NewGuid().ToString(), contentType: _contentType,
                    partitionKey: messageGroupIdTwo)
                {
                    Bag =
                    {
                        [HeaderNames.DeduplicationId] = "123"
                    }
                },
                new MessageBody("test content seven")
            );

            var messageEight = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND,
                    correlationId: Guid.NewGuid().ToString(), contentType: _contentType,
                    partitionKey: messageGroupIdTwo)
                {
                    Bag =
                    {
                        [HeaderNames.DeduplicationId] = "123"
                    }
                },
                new MessageBody("test content eight")
            );

            //send MESSAGE_COUNT messages 
            _fifoMessageProducer.Send(messageOne);
            _fifoMessageProducer.Send(messageTwo);
            _fifoMessageProducer.Send(messageThree);
            _fifoMessageProducer.Send(messageFour);
            _fifoMessageProducer.Send(messageFive);
            _fifoMessageProducer.Send(messageSix);
            _fifoMessageProducer.Send(messageSeven);
            _fifoMessageProducer.Send(messageEight);

            int iteration = 0;
            var messagesReceived = new List<Message>();
            var messagesReceivedCount = messagesReceived.Count;
            do
            {
                iteration++;

                //retrieve  messages
                var messages = _fifoConsumer.Receive(TimeSpan.FromMilliseconds(10000));

                // should not receive more number of message group
                messages.Length.Should().BeLessOrEqualTo(2);

                var moreMessages = messages.Where(m => m.Header.MessageType == MessageType.MT_COMMAND);
                foreach (var message in moreMessages)
                {
                    messagesReceived.Add(message);
                    _fifoConsumer.Acknowledge(message);
                }

                messagesReceivedCount = messagesReceived.Count;

                await Task.Delay(1000);
            } while ((iteration <= 7) && (messagesReceivedCount < _messageCount));


            messagesReceivedCount.Should().Be(7);
        }

        public void Dispose()
        {
            //Clean up resources that we have created
            _channelFactory.DeleteTopic();
            _channelFactory.DeleteQueue();
            
            _fifoChannelFactory.DeleteTopic();
            _fifoChannelFactory.DeleteQueue();
        }
    }
}
