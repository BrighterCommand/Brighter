﻿using System;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.SQS.Model;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Standard
{
    [Trait("Category", "AWS")] 
    public class AWSValidateQueuesTests  : IDisposable, IAsyncDisposable
    {
        private readonly AWSMessagingGatewayConnection _awsConnection;
        private readonly SqsSubscription<MyCommand> _subscription;
        private ChannelFactory _channelFactory;

        public AWSValidateQueuesTests()
        {
            var channelName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
            string topicName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
            var routingKey = new RoutingKey(topicName);
            
            _subscription = new SqsSubscription<MyCommand>(
                name: new SubscriptionName(channelName),
                channelName: new ChannelName(channelName),
                routingKey: routingKey,
                messagePumpType: MessagePumpType.Reactor,
                makeChannels: OnMissingChannel.Validate
            );
            
            (AWSCredentials credentials, RegionEndpoint region) = CredentialsChain.GetAwsCredentials();
            _awsConnection = new AWSMessagingGatewayConnection(credentials, region);
            
            //We need to create the topic at least, to check the queues
            var producer = new SqsMessageProducer(_awsConnection, 
                new SnsPublication
                {
                    MakeChannels = OnMissingChannel.Create 
                });
           producer.ConfirmTopicExistsAsync(topicName).Wait(); 
            
        }

        [Fact]
        public void When_queues_missing_verify_throws()
        {
            //We have no queues so we should throw
            //We need to do this manually in a test - will create the channel from subscriber parameters
            _channelFactory = new ChannelFactory(_awsConnection);
            Assert.Throws<QueueDoesNotExistException>(() => _channelFactory.CreateSyncChannel(_subscription));
        }
 
        public void Dispose()
        {
            _channelFactory.DeleteTopicAsync().Wait(); 
        }
        
        public async ValueTask DisposeAsync()
        {
            await _channelFactory.DeleteTopicAsync(); 
        }
   }
}
