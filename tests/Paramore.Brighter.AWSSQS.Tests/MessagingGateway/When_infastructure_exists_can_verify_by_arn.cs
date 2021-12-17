using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using FluentAssertions;
using Paramore.Brighter.AWSSQS.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWSSQS.Tests.MessagingGateway
{
    [Trait("Category", "AWS")] 
    [Trait("Fragile", "CI")]
    public class AWSValidateInfrastructureByArnTests  : IDisposable
    {     private readonly Message _message;
        private readonly IAmAMessageConsumer _consumer;
        private readonly SqsMessageProducer _messageProducer;
        private readonly ChannelFactory _channelFactory;
        private readonly MyCommand _myCommand;

        public AWSValidateInfrastructureByArnTests()
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
                new MessageBody(JsonSerializer.Serialize((object) _myCommand, JsonSerialisationOptions.Options))
            );


            (AWSCredentials credentials, RegionEndpoint region) = CredentialsChain.GetAwsCredentials();
            var awsConnection = new AWSMessagingGatewayConnection(credentials, region);
            
            //We need to do this manually in a test - will create the channel from subscriber parameters
            //This doesn't look that different from our create tests - this is because we create using the channel factory in
            //our AWS transport, not the consumer (as it's a more likely to use infrastructure declared elsewhere)
            _channelFactory = new ChannelFactory(awsConnection);
            var channel = _channelFactory.CreateChannel(subscription);

            var topicArn = FindTopicArn(credentials, region, routingKey.Value);
            var routingKeyArn = new RoutingKey(topicArn);

            //Now change the subscription to validate, just check what we made
            subscription = new(
                name: new SubscriptionName(channelName),
                channelName: channel.Name,
                routingKey: routingKeyArn,
                findTopicBy: TopicFindBy.Arn,
                makeChannels: OnMissingChannel.Validate
            );
            
            _messageProducer = new SqsMessageProducer(
                awsConnection, 
                new SnsPublication
                {
                    Topic = new RoutingKey(topicName),
                    TopicArn = topicArn,
                    FindTopicBy = TopicFindBy.Arn,
                    MakeChannels = OnMissingChannel.Validate
                });

            _consumer = new SqsMessageConsumerFactory(awsConnection).Create(subscription);
        }

        [Fact]
        public void When_infrastructure_exists_can_verify()
        {
            //arrange
            _messageProducer.Send(_message);

            Task.Delay(1000).Wait();
            
            var messages = _consumer.Receive(5000);
            
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
        
        private string FindTopicArn(AWSCredentials credentials, RegionEndpoint region, string topicName)
        {
            var snsClient = new AmazonSimpleNotificationServiceClient(credentials, region);
            var topicResponse = snsClient.FindTopicAsync(topicName).GetAwaiter().GetResult();
            return topicResponse.TopicArn;
        }
    
   }
}
