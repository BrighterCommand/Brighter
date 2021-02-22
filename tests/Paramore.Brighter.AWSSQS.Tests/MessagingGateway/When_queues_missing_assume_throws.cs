using System;
using System.Linq;
using Amazon;
using Amazon.Runtime;
using Amazon.SQS.Model;
using FluentAssertions;
using Newtonsoft.Json;
using Paramore.Brighter.AWSSQS.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWSSQS.Tests.MessagingGateway
{
    public class AWSAssumeQueuesTests  : IDisposable
    {
        private AWSMessagingGatewayConnection _awsConnection;
        private SqsSubscription<MyCommand> _subscription;
        private SqsMessageProducer _messageProducer;
        private ChannelFactory _channelFactory;
        private SqsMessageConsumer _consumer;

        public AWSAssumeQueuesTests()
        {
            MyCommand myCommand = new MyCommand{Value = "Test"};
            Guid correlationId = Guid.NewGuid();
            var channelName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
            string topicName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
            var routingKey = new RoutingKey(topicName);
            
            _subscription = new SqsSubscription<MyCommand>(
                name: new SubscriptionName(channelName),
                channelName: new ChannelName(channelName),
                routingKey: routingKey,
                makeChannels: OnMissingChannel.Assume
            );
            
            (AWSCredentials credentials, RegionEndpoint region) = CredentialsChain.GetAwsCredentials();
            _awsConnection = new AWSMessagingGatewayConnection(credentials, region);
            
            //We need to create the topic at least, to check the queues
            _messageProducer = new SqsMessageProducer(_awsConnection, new SqsPublication{MakeChannels = OnMissingChannel.Create, RoutingKey = routingKey});
            var sqsQueueName = new ChannelName(channelName).ToValidSQSQueueName();
            _consumer = new SqsMessageConsumer(_awsConnection, sqsQueueName, routingKey);
        }

        [Fact]
        public void When_queues_missing_assume_throws()
        {
            //We have no queues so we should throw
            //We need to do this manually in a test - will create the channel from subscriber parameters
            _channelFactory = new ChannelFactory(_awsConnection);
            
            //This checks the topic but otherwise is a no-op with assume
            var channel = _channelFactory.CreateChannel(_subscription);
            
            //we will try to get the queue url, and fail because it does not exist
            Assert.Throws<QueueDoesNotExistException>(() => _consumer.Receive(1000));
             
        }
 
        public void Dispose()
        {
           _channelFactory.DeleteTopic(); 
        }
        
    
   }
}
