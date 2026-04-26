using System;
using System.Threading.Tasks;
using Amazon.SQS.Model;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using System.Collections.Generic;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Sqs.Standard.Reactor;

[Category("AWS")] 
public class AWSValidateQueuesTests : IAsyncDisposable
{
    private readonly AWSMessagingGatewayConnection _awsConnection;
    private readonly SqsSubscription<MyCommand> _subscription;
    private ChannelFactory _channelFactory;

    public AWSValidateQueuesTests()
    {
        var subscriptionName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var queueName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey(queueName);
            
        _subscription = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(subscriptionName),
            channelName: new ChannelName(queueName),
            channelType: ChannelType.PointToPoint, routingKey: routingKey, messagePumpType: MessagePumpType.Reactor, makeChannels: OnMissingChannel.Validate,
            queueAttributes: new SqsAttributes(tags: new Dictionary<string, string> { { "Environment", "Test" } }));
            
        _awsConnection = GatewayFactory.CreateFactory();
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
        await _channelFactory.DeleteTopicAsync(); 
    }
        
    public async ValueTask DisposeAsync()
    {
        await _channelFactory.DeleteTopicAsync(); 
    }
}
