using System;
using System.Threading.Tasks;
using Amazon.SQS.Model;
using Paramore.Brighter.AWS.V4.Tests.Helpers;
using Paramore.Brighter.AWS.V4.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS.V4;
using Xunit;

namespace Paramore.Brighter.AWS.V4.Tests.MessagingGateway.Sqs.Fifo.Proactor;

[Trait("Category", "AWS")]
public class AwsAssumeQueuesTestsAsync : IAsyncDisposable, IDisposable
{
    private readonly ChannelFactory _channelFactory;
    private readonly IAmAMessageConsumerAsync _consumer;

    public AwsAssumeQueuesTestsAsync()
    {
        var subscriptionName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var queueName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey(queueName);

        var subscription = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(subscriptionName),
            channelName: new ChannelName(queueName),
            channelType: ChannelType.PointToPoint, 
            routingKey: routingKey, 
            queueAttributes: new SqsAttributes(type: SqsType.Fifo), 
            messagePumpType: MessagePumpType.Proactor, 
            makeChannels: OnMissingChannel.Assume);

        var awsConnection = GatewayFactory.CreateFactory();

        _channelFactory = new ChannelFactory(awsConnection);
        _ = _channelFactory.CreateAsyncChannel(subscription);

        //We need to create the topic at least, to check the queues
        _consumer = new SqsMessageConsumerFactory(awsConnection).CreateAsync(subscription);
    }

    [Fact]
    public async Task When_queues_missing_assume_throws_async()
    {
        //we will try to get the queue url, and fail because it does not exist
        await Assert.ThrowsAsync<QueueDoesNotExistException>(async () =>
            await _consumer.ReceiveAsync(TimeSpan.FromMilliseconds(1000)));
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
