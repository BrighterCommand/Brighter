﻿using System;
using System.Threading.Tasks;
using Amazon.SQS.Model;
using Paramore.Brighter.AWS.V4.Tests.Helpers;
using Paramore.Brighter.AWS.V4.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWS.V4;
using Xunit;

namespace Paramore.Brighter.AWS.V4.Tests.MessagingGateway.Sns.Fifo.Reactor;

[Trait("Category", "AWS")]
public class AwsAssumeQueuesTests : IDisposable, IAsyncDisposable
{
    private readonly ChannelFactory _channelFactory;
    private readonly SqsMessageConsumer _consumer;

    public AwsAssumeQueuesTests()
    {
        var channelName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        string topicName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey(topicName);
        var topicAttributes = new SnsAttributes { Type = SqsType.Fifo };

        var subscription = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: new ChannelName(channelName),
            channelType: ChannelType.PubSub,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Reactor,
            queueAttributes: new SqsAttributes(
                type: SqsType.Fifo
            ), 
            topicAttributes: topicAttributes,
            makeChannels: OnMissingChannel.Assume);

        var awsConnection = GatewayFactory.CreateFactory();

        //create the topic, we want the queue to be the issue
        //We need to create the topic at least, to check the queues
        
        var producer = new SnsMessageProducer(awsConnection,
            new SnsPublication
            {
                MakeChannels = OnMissingChannel.Create, TopicAttributes = topicAttributes
            });

        producer.ConfirmTopicExistsAsync(topicName).Wait();

        _channelFactory = new ChannelFactory(awsConnection);
        var channel = _channelFactory.CreateSyncChannel(subscription);

        //We need to create the topic at least, to check the queues
        _consumer = new SqsMessageConsumer(awsConnection, channel.Name.ToValidSQSQueueName());
    }

    [Fact]
    public void When_queues_missing_assume_throws()
    {
        //we will try to get the queue url, and fail because it does not exist
        Assert.Throws<QueueDoesNotExistException>(() => _consumer.Receive(TimeSpan.FromMilliseconds(1000)));
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
