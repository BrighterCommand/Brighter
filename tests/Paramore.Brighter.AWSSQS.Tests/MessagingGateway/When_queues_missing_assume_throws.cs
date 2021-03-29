using System;
using Amazon;
using Amazon.Runtime;
using Amazon.SQS.Model;
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
            
            //create the topic, we want the queue to be the issue
            _messageProducer = new SqsMessageProducer(_awsConnection, new SqsPublication{MakeChannels = OnMissingChannel.Create, RoutingKey = routingKey});
            
            _channelFactory = new ChannelFactory(_awsConnection);
            var channel = _channelFactory.CreateChannel(_subscription);
            
            //We need to create the topic at least, to check the queues
            _messageProducer = new SqsMessageProducer(_awsConnection, new SqsPublication{MakeChannels = OnMissingChannel.Create, RoutingKey = routingKey});
            _consumer = new SqsMessageConsumer(_awsConnection, channel.Name.ToValidSQSQueueName(), routingKey);
        }

        [Fact]
        public void When_queues_missing_assume_throws()
        {
            //we will try to get the queue url, and fail because it does not exist
            Assert.Throws<QueueDoesNotExistException>(() => _consumer.Receive(1000));
        }
 
        public void Dispose()
        {
           _channelFactory.DeleteTopic(); 
        }
        
    
   }
}
