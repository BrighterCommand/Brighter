using System;
using System.Linq;
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
public class AwsValidateInfrastructureTests : IDisposable, IAsyncDisposable
{
    private readonly Message _message;
    private readonly IAmAMessageConsumerSync _consumer;
    private readonly SnsMessageProducer _messageProducer;
    private readonly ChannelFactory _channelFactory;
    private readonly MyCommand _myCommand;

    public AwsValidateInfrastructureTests()
    {
        _myCommand = new MyCommand { Value = "Test" };
        string correlationId = Guid.NewGuid().ToString();
        string replyTo = "http:\\queueUrl";
        var contentType = new ContentType(MediaTypeNames.Text.Plain);
        var channelName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        string topicName = $"Producer-Send-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey(topicName);

        SqsSubscription<MyCommand> subscription = new(
            subscriptionName: new SubscriptionName(channelName),
            channelName: new ChannelName(channelName),
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Create
        );

        _message = new Message(
            new MessageHeader(_myCommand.Id, routingKey, MessageType.MT_COMMAND, correlationId: correlationId,
                replyTo: new RoutingKey(replyTo), contentType: contentType),
            new MessageBody(JsonSerializer.Serialize((object)_myCommand, JsonSerialisationOptions.Options))
        );

        var awsConnection = GatewayFactory.CreateFactory();

        //We need to do this manually in a test - will create the channel from subscriber parameters
        //This doesn't look that different from our create tests - this is because we create using the channel factory in
        //our AWS transport, not the consumer (as it's a more likely to use infrastructure declared elsewhere)
        _channelFactory = new ChannelFactory(awsConnection);
        var channel = _channelFactory.CreateSyncChannel(subscription);

        //Now change the subscription to validate, just check what we made
        subscription.MakeChannels = OnMissingChannel.Validate; 

        _messageProducer = new SnsMessageProducer(
            awsConnection,
            new SnsPublication
            {
                FindTopicBy = TopicFindBy.Name,
                MakeChannels = OnMissingChannel.Validate,
                Topic = new RoutingKey(topicName)
            }
        );

        _consumer = new SqsMessageConsumerFactory(awsConnection).Create(subscription);
    }

    [Fact]
    public async Task When_infrastructure_exists_can_verify()
    {
        //arrange
        _messageProducer.Send(_message);

        await Task.Delay(1000);

        var messages = _consumer.Receive(TimeSpan.FromMilliseconds(5000));

        //Assert
        var message = messages.First();
        Assert.Equal(_myCommand.Id, message.Id);

        //clear the queue
        _consumer.Acknowledge(message);
    }

    public void Dispose()
    {
        //Clean up resources that we have created
        _channelFactory.DeleteTopicAsync().Wait();
        _channelFactory.DeleteQueueAsync().Wait();
        _consumer.Dispose();
        _messageProducer.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _channelFactory.DeleteTopicAsync();
        await _channelFactory.DeleteQueueAsync();
        await ((IAmAMessageConsumerAsync)_consumer).DisposeAsync();
        await _messageProducer.DisposeAsync();
    }
}
