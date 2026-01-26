using System;
using System.Threading.Tasks;
using Paramore.Brighter.Gcp.Tests.Helper;
using Paramore.Brighter.Gcp.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.GcpPubSub;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Stream.Proactor;

[Trait("Category", "GCP")]
public class ValidateQueuesTestsAsync : IDisposable
{
    private readonly GcpPubSubSubscription<MyCommand> _pubSubSubscription;
    private GcpPubSubChannelFactory? _channelFactory;

    public ValidateQueuesTestsAsync()
    {
        var channelName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var topicName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey(topicName);

        _pubSubSubscription = new GcpPubSubSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: new ChannelName(channelName),
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: OnMissingChannel.Validate,
            subscriptionMode: SubscriptionMode.Stream
        );
    }

    [Fact]
    public async Task When_topic_missing_verify_throws_async()
    {
        // We have no topic so we should throw
        // We need to do this manually in a test - will create the channel from subscriber parameters
        _channelFactory = GatewayFactory.CreateChannelFactory();
        await Assert.ThrowsAsync<InvalidOperationException>(() => _channelFactory.CreateAsyncChannelAsync(_pubSubSubscription));
    }
    
    [Fact]
    public async Task When_subscription_missing_verify_throws_async()
    {
        // We have no topic so we should throw
        // We need to do this manually in a test - will create the channel from subscriber parameters
        
        _channelFactory = GatewayFactory.CreateChannelFactory();
        await _channelFactory.EnsureTopicExistAsync(new TopicAttributes
        {
            Name = _pubSubSubscription.RoutingKey, 
            ProjectId = GatewayFactory.GetProjectId()
        }, OnMissingChannel.Create);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _channelFactory.CreateAsyncChannelAsync(_pubSubSubscription));
    }

    public void Dispose()
    {
        if (_channelFactory != null)
        {
            _channelFactory.DeleteTopic(_pubSubSubscription);
            _channelFactory.DeleteSubscription(_pubSubSubscription);
        }
    }
}
