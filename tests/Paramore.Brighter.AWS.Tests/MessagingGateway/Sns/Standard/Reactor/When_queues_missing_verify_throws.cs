using System;
using System.Threading.Tasks;
using Amazon.SQS.Model;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using System.Collections.Generic;
using Amazon.SimpleNotificationService.Model;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Sns.Standard.Reactor;

[Category("AWS")] 
public class AwsValidateQueuesTests : IAsyncDisposable
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

        _subscription = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: new ChannelName(channelName),
            channelType: ChannelType.PubSub,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Validate,
            queueAttributes: new SqsAttributes(tags: new Dictionary<string, string> { { "Environment", "Test" } }),
            topicAttributes: new SnsAttributes(tags: [new Tag { Key = "Environment", Value = "Test" }]));

        _awsConnection = GatewayFactory.CreateFactory();

        //We need to create the topic at least, to check the queues
        var producer = new SnsMessageProducer(_awsConnection,
            new SnsPublication
            {
                MakeChannels = OnMissingChannel.Create
            });
        await producer.ConfirmTopicExistsAsync(topicName);

    }

    [Test]
    public async Task When_queues_missing_verify_throws()
    {
        //We have no queues so we should throw
        //We need to do this manually in a test - will create the channel from subscriber parameters
        _channelFactory = new ChannelFactory(_awsConnection);
        await Assert.That(() => _channelFactory.CreateSyncChannel(_subscription)).ThrowsExactly<QueueDoesNotExistException>();
    }
 
    [After(Test)]
    public async Task Cleanup()
    {
        if (_channelFactory != null)
            await _channelFactory.DeleteTopicAsync(); 
    }
        
    public async ValueTask DisposeAsync()
    {
        if (_channelFactory != null)
            await _channelFactory.DeleteTopicAsync(); 
    }
}
