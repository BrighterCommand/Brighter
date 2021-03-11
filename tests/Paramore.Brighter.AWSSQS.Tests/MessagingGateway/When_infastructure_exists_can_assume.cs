using System;
using System.Linq;
using Amazon;
using Amazon.Runtime;
using FluentAssertions;
using Newtonsoft.Json;
using Paramore.Brighter.AWSSQS.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWSSQS.Tests.MessagingGateway
{
    public class AWSAssumeInfrastructureTests  : IDisposable
    {     private readonly Message _message;
        private readonly SqsMessageConsumer _consumer;
        private readonly SqsMessageProducer _messageProducer;
        private readonly ChannelFactory _channelFactory;
        private readonly MyCommand _myCommand;

        public AWSAssumeInfrastructureTests()
        {
            _myCommand = new MyCommand{Value = "Test"};
            Guid correlationId = Guid.NewGuid();
            string replyTo = "http:\\queueUrl";
            string contentType = "text\\plain";
            var channelName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
            string topicName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
            var routingKey = new RoutingKey(topicName);
            
            SqsSubscription<MyCommand> subscription = new(
                name: new SubscriptionName(channelName),
                channelName: new ChannelName(channelName),
                routingKey: routingKey,
                makeChannels: OnMissingChannel.Create
            );
            
            _message = new Message(
                new MessageHeader(_myCommand.Id, topicName, MessageType.MT_COMMAND, correlationId, replyTo, contentType),
                new MessageBody(JsonConvert.SerializeObject((object) _myCommand))
            );


            (AWSCredentials credentials, RegionEndpoint region) = CredentialsChain.GetAwsCredentials();
            var awsConnection = new AWSMessagingGatewayConnection(credentials, region);
            
            //We need to do this manually in a test - will create the channel from subscriber parameters
            //This doesn't look that different from our create tests - this is because we create using the channel factory in
            //our AWS transport, not the consumer (as it's a more likely to use infrastructure declared elsewhere)
            _channelFactory = new ChannelFactory(awsConnection);
            var channel = _channelFactory.CreateChannel(subscription);
            
            //Now change the subscription to validate, just check what we made
            subscription = new(
                name: new SubscriptionName(channelName),
                channelName: channel.Name,
                routingKey: routingKey,
                makeChannels: OnMissingChannel.Assume
            );
            
            _messageProducer = new SqsMessageProducer(awsConnection, new SqsPublication{MakeChannels = OnMissingChannel.Assume, RoutingKey = routingKey});

            _consumer = new SqsMessageConsumer(awsConnection, channel.Name.ToValidSQSQueueName(), routingKey);
        }

        [Fact]
        public void When_infastructure_exists_can_assume()
        {
            //arrange
            _messageProducer.Send(_message);
            
            var messages = _consumer.Receive(1000);
            
            //Assert
            var message = messages.First();
            message.Id.Should().Be(_myCommand.Id);

            //clear the queue
            _consumer.Acknowledge(message);
        }
 
        public void Dispose()
        {
            _channelFactory.DeleteTopic();
            _channelFactory.DeleteQueue();
            _consumer.Dispose();
            _messageProducer.Dispose();
        }
        
    
   }
}
