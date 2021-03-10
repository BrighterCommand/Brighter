using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using FluentAssertions;
using Paramore.Brighter.AWSSQS.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWSSQS.Tests.MessagingGateway
{
    [Collection("AWS")]
    [Trait("Category", "AWS")]
    public class SQSBufferedConsumerTests : IDisposable
    {
        private readonly SqsMessageProducer _messageProducer;
        private SqsMessageConsumer _consumer;
        private readonly string _topicName; 
        private ChannelFactory _channelFactory;
        private const string CONTENT_TYPE = "text\\plain";
        private const int BUFFER_SIZE = 3;
        private const int MESSAGE_COUNT = 4;

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
                bufferSize: BUFFER_SIZE,
                makeChannels: OnMissingChannel.Create
                ));
            
            //we want to access via a consumer, to receive multiple messages - we don't want to expose on channel
            //just for the tests, so create a new consumer from the properties
            _consumer = new SqsMessageConsumer(awsConnection, channel.Name.ToValidSQSQueueName(), routingKey, BUFFER_SIZE);
            _messageProducer = new SqsMessageProducer(awsConnection, 
                new SqsPublication
                {
                    MakeChannels = OnMissingChannel.Create, 
                    RoutingKey = routingKey
                });
        }
            
        [Fact]
        public void When_a_message_consumer_reads_multiple_messages()
        {
            var messageOne = new Message(
                new MessageHeader(Guid.NewGuid(), _topicName, MessageType.MT_COMMAND, Guid.NewGuid(), string.Empty, CONTENT_TYPE),
                new MessageBody("test content one")
                );
            
            var messageTwo= new Message(
                new MessageHeader(Guid.NewGuid(), _topicName, MessageType.MT_COMMAND, Guid.NewGuid(), string.Empty, CONTENT_TYPE),
                new MessageBody("test content two")
                );
           
            var messageThree= new Message(
                new MessageHeader(Guid.NewGuid(), _topicName, MessageType.MT_COMMAND, Guid.NewGuid(), string.Empty, CONTENT_TYPE),
                new MessageBody("test content three")
                );
             
            var messageFour= new Message(
                new MessageHeader(Guid.NewGuid(), _topicName, MessageType.MT_COMMAND, Guid.NewGuid(), string.Empty, CONTENT_TYPE),
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
                var outstandingMessageCount = MESSAGE_COUNT - messagesReceivedCount;

                //retrieve  messages
                var messages = _consumer.Receive(10000);
                
                messages.Length.Should().BeLessOrEqualTo(outstandingMessageCount);
                
                //should not receive more than buffer in one hit
                messages.Length.Should().BeLessOrEqualTo(BUFFER_SIZE);

                var moreMessages = messages.Where(m => m.Header.MessageType == MessageType.MT_COMMAND);
                foreach (var message in moreMessages)
                {
                    messagesReceived.Add(message);
                   _consumer.Acknowledge(message);
                }
                 
                messagesReceivedCount = messagesReceived.Count;
                
                Task.Delay(1000).Wait();

            } while ((iteration <= 5) && (messagesReceivedCount <  MESSAGE_COUNT));
    

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


