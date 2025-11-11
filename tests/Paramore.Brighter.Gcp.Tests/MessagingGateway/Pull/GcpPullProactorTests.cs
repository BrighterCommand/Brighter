using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Base.Test.MessagingGateway;
using Paramore.Brighter.Base.Test.Requests;
using Paramore.Brighter.Gcp.Tests.Helper;
using Paramore.Brighter.MessagingGateway.GcpPubSub;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Pull;

[Collection("PullProactor")]
public class GcpPullProactorTests : MessagingGatewayProactorTests<GcpPublication, GcpPubSubSubscription>
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

    protected override ChannelName GetOrCreateChannelName(string testName = null!)
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
            MakeChannels = OnMissingChannel.Create
        };
    }

    protected override GcpPubSubSubscription CreateSubscription(RoutingKey routingKey, 
        ChannelName channelName,
        OnMissingChannel makeChannel = OnMissingChannel.Create,
        bool setupDeadLetterQueue = false)
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
            requeueCount: 6,
            enableMessageOrdering: channelName.Value.StartsWith("PartitionKey"),
            messagePumpType: MessagePumpType.Proactor,
            subscriptionMode: SubscriptionMode.Pull);
    }

    protected override async Task<IAmAMessageProducerAsync> CreateProducerAsync(GcpPublication publication, CancellationToken cancellationToken = default)
    {
        var producers = await new GcpPubSubMessageProducerFactory(GatewayFactory.CreateFactory(), [publication])
            .CreateAsync();
        
        var producer = producers.First().Value;
        return (IAmAMessageProducerAsync)producer;
    }

    protected override async Task<IAmAChannelAsync> CreateChannelAsync(GcpPubSubSubscription subscription, CancellationToken cancellationToken = default)
    {
        var channel = await new GcpPubSubChannelFactory(GatewayFactory.CreateFactory())
            .CreateAsyncChannelAsync(subscription, cancellationToken);

        if (subscription.MakeChannels == OnMissingChannel.Create)
        {
            // Ensuring that the queue exists before return the channel
            await channel.ReceiveAsync(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        return channel;
    }

    protected override async Task<Message> GetMessageFromDeadLetterQueueAsync(GcpPubSubSubscription subscription,
        CancellationToken cancellationToken = default)
    {
        var sub = new GcpPubSubSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(Uuid.New().ToString("N")),
            routingKey: subscription.DeadLetter!.TopicName,
            channelName: subscription.DeadLetter!.Subscription,
            makeChannels: OnMissingChannel.Assume,
            messagePumpType: MessagePumpType.Proactor,
            subscriptionMode: SubscriptionMode.Pull);
        using var channel = await CreateChannelAsync(sub, cancellationToken);
        
        for (var i = 0; i < 10; i++)
        {
            var message = await channel.ReceiveAsync(ReceiveTimeout, cancellationToken);
            if (message.Header.MessageType != MessageType.MT_NONE)
            {
                return message;
            }
        }

        return new Message();
    }

    protected override async Task CleanUpAsync(CancellationToken cancellationToken = default)
    {
        if (Subscription != null)
        {
            var factory = new GcpPubSubChannelFactory(GatewayFactory.CreateFactory());
            await factory.DeleteSubscriptionAsync(Subscription, cancellationToken);
            await factory.DeleteTopicAsync(Subscription, cancellationToken);
        }
    }
}
