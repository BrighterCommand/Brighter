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
    public class SQSBufferedConsumerTests : IDisposable, IAsyncDisposable
    {
        private readonly SqsMessageProducer _messageProducer;
        private readonly SqsMessageConsumer _consumer;
        private readonly string _topicName; 
        private readonly ChannelFactory _channelFactory;
        private const string ContentType = "text\\plain";
        private const int BufferSize = 3;
        private const int MessageCount = 4;

        public SQSBufferedConsumerTests()
        {
            (AWSCredentials credentials, RegionEndpoint region) = CredentialsChain.GetAwsCredentials();
            var awsConnection = new AWSMessagingGatewayConnection(credentials, region);

            _channelFactory = new ChannelFactory(awsConnection);
            var channelName = $"Buffered-Consumer-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
            _topicName = $"Buffered-Consumer-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
                
            //we need the channel to create the queues and notifications
            var routingKey = new RoutingKey(_topicName);
            
            var channel = _channelFactory.CreateSyncChannel(new SqsSubscription<MyCommand>(
                name: new SubscriptionName(channelName),
                channelName:new ChannelName(channelName),
                routingKey:routingKey,
                bufferSize: BufferSize,
                makeChannels: OnMissingChannel.Create
                ));
            
            //we want to access via a consumer, to receive multiple messages - we don't want to expose on channel
            //just for the tests, so create a new consumer from the properties
            _consumer = new SqsMessageConsumer(awsConnection, channel.Name.ToValidSQSQueueName(), BufferSize);
            _messageProducer = new SqsMessageProducer(awsConnection, 
                new SnsPublication
                {
                    MakeChannels = OnMissingChannel.Create 
                });
        }
            
        [Fact]
        public async Task When_a_message_consumer_reads_multiple_messages()
        {
            var routingKey = new RoutingKey(_topicName);
            
            var messageOne = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND, 
                    correlationId: Guid.NewGuid().ToString(), contentType: ContentType),
                new MessageBody("test content one")
                );
            
            var messageTwo= new Message(
                new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND, 
                    correlationId: Guid.NewGuid().ToString(), contentType: ContentType),
                new MessageBody("test content two")
                );
           
            var messageThree= new Message(
                new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND, 
                    correlationId: Guid.NewGuid().ToString(), contentType: ContentType),
                new MessageBody("test content three")
                );
             
            var messageFour= new Message(
                new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND, 
                    correlationId: Guid.NewGuid().ToString(), contentType: ContentType),
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
                var outstandingMessageCount = MessageCount - messagesReceivedCount;

                //retrieve  messages
                var messages = _consumer.Receive(TimeSpan.FromMilliseconds(10000));
                
                messages.Length.Should().BeLessOrEqualTo(outstandingMessageCount);
                
                //should not receive more than buffer in one hit
                messages.Length.Should().BeLessOrEqualTo(BufferSize);

                var moreMessages = messages.Where(m => m.Header.MessageType == MessageType.MT_COMMAND);
                foreach (var message in moreMessages)
                {
                    messagesReceived.Add(message);
                   _consumer.Acknowledge(message);
                }
                 
                messagesReceivedCount = messagesReceived.Count;
                
                await Task.Delay(1000);

            } while ((iteration <= 5) && (messagesReceivedCount <  MessageCount));
    

            messagesReceivedCount.Should().Be(4);

        }
        
        public void Dispose()
        {
            //Clean up resources that we have created
            _channelFactory.DeleteTopicAsync().Wait();
            _channelFactory.DeleteQueueAsync().Wait();
            _messageProducer.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            await _channelFactory.DeleteTopicAsync();
            await _channelFactory.DeleteQueueAsync();
            await ((IAmAMessageProducerAsync) _messageProducer).DisposeAsync();
        }
    }
}


