﻿using System;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.SQS.Model;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway
{
    [Trait("Category", "AWS")]
    public class AWSValidateQueuesTestsAsync : IAsyncDisposable
    {
        private readonly AWSMessagingGatewayConnection _awsConnection;
        private readonly SqsSubscription<MyCommand> _subscription;
        private ChannelFactory _channelFactory;

        public AWSValidateQueuesTestsAsync()
        {
            var channelName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
            string topicName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
            var routingKey = new RoutingKey(topicName);

            _subscription = new SqsSubscription<MyCommand>(
                name: new SubscriptionName(channelName),
                channelName: new ChannelName(channelName),
                routingKey: routingKey,
                makeChannels: OnMissingChannel.Validate
            );

            (AWSCredentials credentials, RegionEndpoint region) = CredentialsChain.GetAwsCredentials();
            _awsConnection = new AWSMessagingGatewayConnection(credentials, region);

            // We need to create the topic at least, to check the queues
            var producer = new SqsMessageProducer(_awsConnection,
                new SnsPublication
                {
                    MakeChannels = OnMissingChannel.Create
                });
            producer.ConfirmTopicExistsAsync(topicName).Wait();
        }

        [Fact]
        public async Task When_queues_missing_verify_throws_async()
        {
            // We have no queues so we should throw
            // We need to do this manually in a test - will create the channel from subscriber parameters
            _channelFactory = new ChannelFactory(_awsConnection);
            await Assert.ThrowsAsync<QueueDoesNotExistException>(async () => await _channelFactory.CreateAsyncChannelAsync(_subscription));
        }

        public async ValueTask DisposeAsync()
        {
            await _channelFactory.DeleteTopicAsync();
            GC.SuppressFinalize(this);
        }
    }
}
