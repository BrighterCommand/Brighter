using System;
using Paramore.Brighter.Gcp.Tests.Helper;
using Paramore.Brighter.Gcp.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.GcpPubSub;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Stream.Reactor;

[Trait("Category", "GCP")]
public class StreamValidateQueuesTestsAsync : IDisposable
{
    private readonly GcpMessagingGatewayConnection _connection;
    private readonly GcpSubscription<MyCommand> _subscription;
    private GcpPubSubChannelFactory? _channelFactory;

    public StreamValidateQueuesTestsAsync()
    {
        var channelName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var topicName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey(topicName);

        _subscription = new GcpSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: new ChannelName(channelName),
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Validate,
            subscriptionMode: SubscriptionMode.Stream
        );

        _connection = GatewayFactory.CreateFactory();
    }

    [Fact]
    public void When_topic_missing_verify_throws()
    {
        // We have no topic so we should throw
        // We need to do this manually in a test - will create the channel from subscriber parameters
        _channelFactory = new GcpPubSubChannelFactory(_connection);
        Assert.Throws<InvalidOperationException>(() => _channelFactory.CreateSyncChannel(_subscription));
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
