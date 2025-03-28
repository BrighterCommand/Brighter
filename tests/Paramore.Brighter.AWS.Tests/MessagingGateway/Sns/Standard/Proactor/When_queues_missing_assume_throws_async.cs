using System;
using System.Threading.Tasks;
using Amazon.SQS.Model;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Sns.Standard.Proactor;

[Trait("Category", "AWS")]
public class AWSAssumeQueuesTestsAsync : IAsyncDisposable, IDisposable
{
    private readonly ChannelFactory _channelFactory;
    private readonly IAmAMessageConsumerAsync _consumer;

    public AWSAssumeQueuesTestsAsync()
    {
        var channelName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        string topicName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey(topicName);

        var subscription = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: new ChannelName(channelName),
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Proactor, 
            makeChannels: OnMissingChannel.Assume);

        var awsConnection = GatewayFactory.CreateFactory();

        //create the topic, we want the queue to be the issue
        //We need to create the topic at least, to check the queues
        var producer = new SnsMessageProducer(awsConnection,
            new SnsPublication
            {
                MakeChannels = OnMissingChannel.Create
            });

        producer.ConfirmTopicExistsAsync(topicName).Wait();
        
        //TODO: Assume will fail here, as so we have no Arn for the topic

        _channelFactory = new ChannelFactory(awsConnection);
        var channel = _channelFactory.CreateAsyncChannel(subscription);
        
        
        //TODO: for assume, does this call need to be before create channel? Or after?

        _consumer = new SqsMessageConsumerFactory(awsConnection).CreateAsync(subscription);
    }

    [Fact]
    public async Task When_queues_missing_assume_throws_async()
    {
        //we will try to get the queue url, and fail because it does not exist
        await Assert.ThrowsAsync<QueueDoesNotExistException>(async () => await _consumer.ReceiveAsync(TimeSpan.FromMilliseconds(1000)));
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
