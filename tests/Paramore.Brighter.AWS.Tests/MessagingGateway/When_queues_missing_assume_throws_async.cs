using System;
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
    public class AWSAssumeQueuesTestsAsync : IAsyncDisposable, IDisposable
    {
        private readonly ChannelFactory _channelFactory;
        private readonly IAmAMessageConsumerAsync _consumer;

        public AWSAssumeQueuesTestsAsync()
        {
            var channelName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
            string topicName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
            var routingKey = new RoutingKey(topicName);

            var subscription = new SqsSubscription<MyCommand>(
                name: new SubscriptionName(channelName),
                channelName: new ChannelName(channelName),
                routingKey: routingKey,
                makeChannels: OnMissingChannel.Assume,
                messagePumpType: MessagePumpType.Proactor
            );

            (AWSCredentials credentials, RegionEndpoint region) = CredentialsChain.GetAwsCredentials();
            var awsConnection = new AWSMessagingGatewayConnection(credentials, region);

            //create the topic, we want the queue to be the issue
            //We need to create the topic at least, to check the queues
            var producer = new SqsMessageProducer(awsConnection,
                new SnsPublication
                {
                    MakeChannels = OnMissingChannel.Create
                });

            producer.ConfirmTopicExistsAsync(topicName).Wait();

            _channelFactory = new ChannelFactory(awsConnection);
            var channel = _channelFactory.CreateAsyncChannel(subscription);

            //We need to create the topic at least, to check the queues
            _consumer = new SqsMessageConsumerFactory(awsConnection).CreateAsync(subscription);
        }

        [Fact]
        public async Task When_queues_missing_assume_throws_async()
        {
            //we will try to get the queue url, and fail because it does not exist
            await Assert.ThrowsAsync<QueueDoesNotExistException>(async () => await _consumer.ReceiveAsync(TimeSpan.FromMilliseconds(1000)));
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
