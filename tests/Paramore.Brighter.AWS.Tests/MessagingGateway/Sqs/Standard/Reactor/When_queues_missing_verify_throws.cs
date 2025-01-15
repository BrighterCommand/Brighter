using System;
using System.Threading.Tasks;
using Amazon.SQS.Model;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Sqs.Standard.Reactor;

[Trait("Category", "AWS")] 
public class AWSValidateQueuesTests  : IDisposable, IAsyncDisposable
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
            name: new SubscriptionName(subscriptionName),
            channelName: new ChannelName(queueName),
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Validate,
            channelType: ChannelType.PointToPoint
        );
            
        _awsConnection = GatewayFactory.CreateFactory();
    }

    [Fact]
    public void When_queues_missing_verify_throws()
    {
        //We have no queues so we should throw
        //We need to do this manually in a test - will create the channel from subscriber parameters
        _channelFactory = new ChannelFactory(_awsConnection);
        Assert.Throws<QueueDoesNotExistException>(() => _channelFactory.CreateSyncChannel(_subscription));
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
