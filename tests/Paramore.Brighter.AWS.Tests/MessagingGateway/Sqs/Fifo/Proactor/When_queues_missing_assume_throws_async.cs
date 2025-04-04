﻿using System;
using System.Threading.Tasks;
using Amazon.SQS.Model;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Sqs.Fifo.Proactor;

[Trait("Category", "AWS")]
public class AWSAssumeQueuesTestsAsync : IAsyncDisposable, IDisposable
{
    private readonly ChannelFactory _channelFactory;
    private readonly IAmAMessageConsumerAsync _consumer;

    public AWSAssumeQueuesTestsAsync()
    {
        var subscriptionName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var queueName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey(queueName);

        var subscription = new SqsSubscription<MyCommand>(
            name: new SubscriptionName(subscriptionName),
            channelName: new ChannelName(queueName),
            routingKey: routingKey,
            makeChannels: OnMissingChannel.Assume,
            messagePumpType: MessagePumpType.Proactor,
            sqsType: SnsSqsType.Fifo,
            channelType: ChannelType.PointToPoint
        );

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
