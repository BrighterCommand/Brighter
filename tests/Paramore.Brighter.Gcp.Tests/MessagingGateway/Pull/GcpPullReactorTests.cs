using System;
using System.Linq;
using Paramore.Brighter.Base.Test.MessagingGateway;
using Paramore.Brighter.Base.Test.MessagingGateway.Reactor;
using Paramore.Brighter.Base.Test.Requests;
using Paramore.Brighter.Gcp.Tests.Helper;
using Paramore.Brighter.MessagingGateway.GcpPubSub;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Pull;

[Collection("PullReactor")]
public class GcpPullReactorTests : MessagingGatewayReactorTests<GcpPublication, GcpPubSubSubscription>
{
    protected override bool HasSupportToDeadLetterQueue => true;
    protected override bool HasSupportToMoveToDeadLetterQueueAfterTooManyRetries => true;
    protected override bool HasSupportToPartitionKey => true; 
    
    protected override RoutingKey GetOrCreateRoutingKey(string testName = null!)
    {
        if (testName.Contains("partition", StringComparison.InvariantCultureIgnoreCase))
        {
            return new RoutingKey($"PartitionKey{Uuid.New():N}");
        }
        
        return base.GetOrCreateRoutingKey(testName);
    }

    protected override ChannelName GetOrCreateChannelName(string testName = null)
    {
        if (testName.Contains("partition", StringComparison.InvariantCultureIgnoreCase))
        {
            return new ChannelName($"PartitionKey{Uuid.New():N}");
        }
        
        return base.GetOrCreateChannelName(testName);
    }

    protected override GcpPublication CreatePublication(RoutingKey routingKey)
    {
        return new GcpPublication<MyCommand>
        {
            Topic = routingKey, 
            EnableMessageOrdering = routingKey.Value.StartsWith("PartitionKey"),
            MakeChannels = OnMissingChannel.Create,
        };
    }

    protected override GcpPubSubSubscription CreateSubscription(RoutingKey routingKey, ChannelName channelName,
        OnMissingChannel makeChannel = OnMissingChannel.Create, bool setupDeadLetterQueue = false)
    {
        DeadLetterPolicy? deadLetter = null;
        if (setupDeadLetterQueue)
        {
            deadLetter = new DeadLetterPolicy($"{routingKey}DLQ", $"{channelName}DLQ")
            {
                MaxDeliveryAttempts = 5
            };
        }
        
        return new GcpPubSubSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(Uuid.New().ToString("N")),
            routingKey: routingKey,
            channelName: channelName,
            deadLetter: deadLetter,
            enableMessageOrdering: channelName.Value.StartsWith("Partition"),
            requeueCount: 6,
            messagePumpType: MessagePumpType.Reactor,
            subscriptionMode: SubscriptionMode.Pull);
    }

    protected override IAmAMessageProducerSync CreateProducer(GcpPublication publication)
    {
        var producers = new GcpPubSubMessageProducerFactory(GatewayFactory.CreateFactory(), [publication])
            .Create();
        
        var producer = producers.First().Value;
        return (IAmAMessageProducerSync)producer;
    }

    protected override IAmAChannelSync CreateChannel(GcpPubSubSubscription subscription)
    {
        var channel = new GcpPubSubChannelFactory(GatewayFactory.CreateFactory())
            .CreateSyncChannel(subscription);

        if (subscription.MakeChannels == OnMissingChannel.Create)
        {
            // Ensuring that the queue exists before return the channel
            channel.Receive(TimeSpan.FromMilliseconds(100));
        }

        return channel;
    }

    protected override Message GetMessageFromDeadLetterQueue(GcpPubSubSubscription subscription)
    {
        var sub = new GcpPubSubSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(Uuid.New().ToString("N")),
            routingKey: subscription.DeadLetter!.TopicName,
            channelName: subscription.DeadLetter!.Subscription,
            messagePumpType: MessagePumpType.Proactor,
            subscriptionMode: SubscriptionMode.Pull);
        using var channel = CreateChannel(sub);
        
        for (var i = 0; i < 10; i++)
        {
            var message = channel.Receive(ReceiveTimeout);
            if (message.Header.MessageType != MessageType.MT_NONE)
            {
                return message;
            }
        }

        return new Message();
    }

    protected override void CleanUp()
    {
        if (Subscription != null)
        {
            var factory = new GcpPubSubChannelFactory(GatewayFactory.CreateFactory());
            factory.DeleteSubscription(Subscription);
            factory.DeleteTopic(Subscription);
        }
    }
}
