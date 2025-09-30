using System;
using Paramore.Brighter.Gcp.Tests.Helper;
using Paramore.Brighter.Gcp.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.GcpPubSub;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Pull.Reactor;

[Trait("Category", "GCP")]
public class ValidateQueuesTestsAsync : IDisposable
{
    private readonly GcpMessagingGatewayConnection _connection;
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
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Validate,
            subscriptionMode: SubscriptionMode.Pull
        );

        _connection = GatewayFactory.CreateFactory();
    }

    [Fact]
    public void When_topic_missing_verify_throws()
    {
        // We have no topic so we should throw
        // We need to do this manually in a test - will create the channel from subscriber parameters
        _channelFactory = GatewayFactory.CreateChannelFactory();
        Assert.Throws<InvalidOperationException>(() => _channelFactory.CreateSyncChannel(_pubSubSubscription));
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
