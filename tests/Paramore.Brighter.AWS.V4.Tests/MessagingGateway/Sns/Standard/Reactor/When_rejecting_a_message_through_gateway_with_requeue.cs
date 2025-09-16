using System;
using System.Net.Mime;
using System.Text.Json;
using System.Threading.Tasks;
using Paramore.Brighter.AWS.V4.Tests.Helpers;
using Paramore.Brighter.AWS.V4.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.AWSSQS.V4;
using Xunit;

namespace Paramore.Brighter.AWS.V4.Tests.MessagingGateway.Sns.Standard.Reactor;

[Trait("Category", "AWS")]
[Trait("Fragile", "CI")]
public class SqsMessageConsumerRequeueTests : IDisposable
{
    private readonly Message _message;
    private readonly IAmAChannelSync _channel;
    private readonly SnsMessageProducer _messageProducer;
    private readonly ChannelFactory _channelFactory;
    private readonly MyCommand _myCommand;

    public SqsMessageConsumerRequeueTests()
    {
        _myCommand = new MyCommand{Value = "Test"};
        string correlationId = Guid.NewGuid().ToString();
        string replyTo = "http:\\queueUrl";
        var contentType = new ContentType(MediaTypeNames.Text.Plain);
        var channelName = $"Consumer-Requeue-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        string topicName = $"Consumer-Requeue-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey(topicName);
            
        SqsSubscription<MyCommand> subscription = new(
            subscriptionName: new SubscriptionName(channelName),
            channelName: new ChannelName(channelName),
            channelType: ChannelType.PubSub,
            routingKey: routingKey, 
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Create);
            
        _message = new Message(
            new MessageHeader(_myCommand.Id, routingKey, MessageType.MT_COMMAND, correlationId: correlationId,
                replyTo: new RoutingKey(replyTo), contentType: contentType),
            new MessageBody(JsonSerializer.Serialize((object) _myCommand, JsonSerialisationOptions.Options))
        );
            
        //Must have credentials stored in the SDK Credentials store or shared credentials file
        var awsConnection = GatewayFactory.CreateFactory();
            
        //We need to do this manually in a test - will create the channel from subscriber parameters
        _channelFactory = new ChannelFactory(awsConnection);
        _channel = _channelFactory.CreateSyncChannel(subscription);
            
        _messageProducer = new SnsMessageProducer(awsConnection, new SnsPublication{MakeChannels = OnMissingChannel.Create});
    }

    [Fact]
    public void When_rejecting_a_message_through_gateway_with_requeue()
    {
        _messageProducer.Send(_message);

        var message = _channel.Receive(TimeSpan.FromMilliseconds(5000));
            
        _channel.Reject(message);

        //Let the timeout change
        Task.Delay(TimeSpan.FromMilliseconds(3000));

        //should requeue_the_message
        message = _channel.Receive(TimeSpan.FromMilliseconds(5000));
            
        //clear the queue
        _channel.Acknowledge(message);

        Assert.Equal(_myCommand.Id, message.Id);
    }

    public void Dispose()
    {
        _channelFactory.DeleteTopicAsync().Wait(); 
        _channelFactory.DeleteQueueAsync().Wait();
    }
        
    public async ValueTask DisposeAsync()
    {
        await _channelFactory.DeleteTopicAsync(); 
        await _channelFactory.DeleteQueueAsync();
    }
}
