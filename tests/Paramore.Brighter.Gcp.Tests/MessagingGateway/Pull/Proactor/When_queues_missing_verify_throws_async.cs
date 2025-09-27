using System;
using System.Threading.Tasks;
using Paramore.Brighter.Gcp.Tests.Helper;
using Paramore.Brighter.Gcp.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.GcpPubSub;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Pull.Proactor;

[Trait("Category", "GCP")]
public class ValidateQueuesTestsAsync : IDisposable
{
    private readonly GcpMessagingGatewayConnection _connection;
    private readonly GcpSubscription<MyCommand> _subscription;
    private GcpPubSubChannelFactory? _channelFactory;

    public ValidateQueuesTestsAsync()
    {
        var channelName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var topicName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey(topicName);

        _subscription = new GcpSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: new ChannelName(channelName),
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: OnMissingChannel.Validate,
            subscriptionMode: SubscriptionMode.Pull
        );

        _connection = GatewayFactory.CreateFactory();
    }

    [Fact]
    public async Task When_topic_missing_verify_throws_async()
    {
        // We have no topic so we should throw
        // We need to do this manually in a test - will create the channel from subscriber parameters
        _channelFactory = new GcpPubSubChannelFactory(_connection);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await _channelFactory.CreateAsyncChannelAsync(_subscription));
    }
    
    [Fact]
    public async Task When_subscription_missing_verify_throws_async()
    {
        // We have no topic so we should throw
        // We need to do this manually in a test - will create the channel from subscriber parameters
        
        _channelFactory = new GcpPubSubChannelFactory(_connection);
        await _channelFactory.EnsureTopicExistAsync(new TopicAttributes
        {
            Name = _subscription.RoutingKey, 
            ProjectId = _connection.ProjectId
        }, OnMissingChannel.Create);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await _channelFactory.CreateAsyncChannelAsync(_subscription));
    }

    public void Dispose()
    {
        if (_channelFactory != null)
        {
            _channelFactory.DeleteTopic(_subscription);
            _channelFactory.DeleteSubscription(_subscription);
        }
    }
}
