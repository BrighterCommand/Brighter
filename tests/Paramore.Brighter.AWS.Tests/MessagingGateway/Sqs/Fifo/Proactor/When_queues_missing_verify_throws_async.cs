using System;
using System.Threading.Tasks;
using Amazon.SQS.Model;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Sqs.Fifo.Proactor;

[Trait("Category", "AWS")]
public class AWSValidateQueuesTestsAsync : IAsyncDisposable
{
    private readonly AWSMessagingGatewayConnection _awsConnection;
    private readonly SqsSubscription<MyCommand> _subscription;
    private ChannelFactory _channelFactory;

    public AWSValidateQueuesTestsAsync()
    {
        var subscriptionName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var queueName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey(queueName);

        _subscription = new SqsSubscription<MyCommand>(
            name: new SubscriptionName(subscriptionName),
            channelName: new ChannelName(queueName),
            routingKey: routingKey,
            makeChannels: OnMissingChannel.Validate,
            sqsType: SnsSqsType.Fifo,
            routingKeyType: RoutingKeyType.PointToPoint
        );

        _awsConnection = GatewayFactory.CreateFactory();
    }

    [Fact]
    public async Task When_queues_missing_verify_throws_async()
    {
        // We have no queues so we should throw
        // We need to do this manually in a test - will create the channel from subscriber parameters
        _channelFactory = new ChannelFactory(_awsConnection);
        await Assert.ThrowsAsync<QueueDoesNotExistException>(async () =>
            await _channelFactory.CreateAsyncChannelAsync(_subscription));
    }

    public async ValueTask DisposeAsync()
    {
        await _channelFactory.DeleteTopicAsync();
    }
}
