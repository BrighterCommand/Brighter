using System;
using System.Threading.Tasks;
using Amazon.SQS.Model;
using Paramore.Brighter.AWS.V4.Tests.Helpers;
using Paramore.Brighter.AWS.V4.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS.V4;
using System.Collections.Generic;

namespace Paramore.Brighter.AWS.V4.Tests.MessagingGateway.Sns.Fifo.Proactor;

[Category("AWS")]
public class AwsValidateQueuesTestsAsync : IAsyncDisposable
{
    private AWSMessagingGatewayConnection _awsConnection;
    private SqsSubscription<MyCommand> _subscription;
    private ChannelFactory? _channelFactory;

    [Before(Test)]
    public async Task Setup()
    {
        var channelName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        string topicName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey(topicName);
        var topicAttributes = new SnsAttributes { Type = SqsType.Fifo };

        _subscription = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: new ChannelName(channelName),
            channelType: ChannelType.PubSub,
            routingKey: routingKey,
            queueAttributes: new SqsAttributes( type: SqsType.Fifo, tags: new Dictionary<string, string> { { "Environment", "Test" } }),
            topicAttributes: topicAttributes,
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: OnMissingChannel.Validate);

        _awsConnection = GatewayFactory.CreateFactory();

        // We need to create the topic at least, to check the queues
        var producer = new SnsMessageProducer(_awsConnection,
            new SnsPublication
            {
                MakeChannels = OnMissingChannel.Create, TopicAttributes = topicAttributes
            });
        await producer.ConfirmTopicExistsAsync(topicName);
    }

    [Test]
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
