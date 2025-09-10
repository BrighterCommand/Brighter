using System;
using System.Threading.Tasks;
using Amazon.SQS.Model;
using Paramore.Brighter.AWS.V4.Tests.Helpers;
using Paramore.Brighter.AWS.V4.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS.V4;
using Xunit;

namespace Paramore.Brighter.AWS.V4.Tests.MessagingGateway.Sns.Standard.Reactor;

[Trait("Category", "AWS")] 
public class AwsValidateQueuesTests  : IDisposable, IAsyncDisposable
{
    private readonly AWSMessagingGatewayConnection _awsConnection;
    private readonly SqsSubscription<MyCommand> _subscription;
    private ChannelFactory? _channelFactory;

    public AwsValidateQueuesTests()
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
            makeChannels: OnMissingChannel.Validate
        );
            
        _awsConnection = GatewayFactory.CreateFactory();
            
        //We need to create the topic at least, to check the queues
        var producer = new SnsMessageProducer(_awsConnection, 
            new SnsPublication
            {
                MakeChannels = OnMissingChannel.Create 
            });
        producer.ConfirmTopicExistsAsync(topicName).Wait(); 
            
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
        if (_channelFactory != null)
            _channelFactory.DeleteTopicAsync().Wait(); 
    }
        
    public async ValueTask DisposeAsync()
    {
        if (_channelFactory != null)
            await _channelFactory.DeleteTopicAsync(); 
    }
}
