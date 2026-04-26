using System;
using System.Threading.Tasks;
using Amazon.SQS.Model;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using System.Collections.Generic;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Sqs.Standard.Proactor;

[Category("AWS")]
public class AWSAssumeQueuesTestsAsync : IAsyncDisposable
{
    private ChannelFactory _channelFactory;
    private IAmAMessageConsumerAsync _consumer;

    [Before(Test)]
    public async Task Setup()
    {
        var channelName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var queueName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey(queueName);

        var subscription = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: new ChannelName(queueName),
            channelType: ChannelType.PointToPoint, routingKey: routingKey, messagePumpType: MessagePumpType.Proactor, makeChannels: OnMissingChannel.Assume,
            queueAttributes: new SqsAttributes(tags: new Dictionary<string, string> { { "Environment", "Test" } }));

        var awsConnection = GatewayFactory.CreateFactory();

        //create the topic, we want the queue to be the issue
        //We need to create the topic at least, to check the queues
        var producer = new SnsMessageProducer(awsConnection,
            new SnsPublication
            {
                MakeChannels = OnMissingChannel.Create
            });

        await producer.ConfirmTopicExistsAsync(queueName);

        _channelFactory = new ChannelFactory(awsConnection);
        var channel = await _channelFactory.CreateAsyncChannelAsync(subscription);

        //We need to create the topic at least, to check the queues
        _consumer = new SqsMessageConsumerFactory(awsConnection).CreateAsync(subscription);
    }

    [Test]
    public async Task When_queues_missing_assume_throws_async()
    {
        //we will try to get the queue url, and fail because it does not exist
        await Assert.That(() => _consumer.ReceiveAsync(TimeSpan.FromMilliseconds(1000))).ThrowsExactly<QueueDoesNotExistException>();
    }
        
    [After(Test)]
    public async Task Cleanup()
    {
        await _channelFactory.DeleteTopicAsync(); 
    }

    public async ValueTask DisposeAsync()
    {
        await _channelFactory.DeleteTopicAsync();
    }
}
