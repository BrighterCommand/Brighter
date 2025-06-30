using System;
using System.Net.Mime;
using System.Text.Json;
using System.Threading.Tasks;
using Paramore.Brighter.Gcp.Tests.Helper;
using Paramore.Brighter.Gcp.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.GcpPubSub;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Proactor;

[Trait("Category", "GCP")]
[Trait("Fragile", "CI")]
public class MessageConsumerRequeueTestsAsync : IAsyncDisposable
{
    private readonly Message _message;
    private readonly IAmAChannelAsync _channel;
    private readonly GcpMessageProducer _messageProducer;
    private readonly GcpPubSubChannelFactory _channelFactory;
    private readonly GcpSubscription<MyCommand> _subscription;
    private readonly MyCommand _myCommand;

    public MessageConsumerRequeueTestsAsync()
    {
        _myCommand = new MyCommand { Value = "Test" };
        string correlationId = Guid.NewGuid().ToString();
        const string replyTo = "http:\\queueUrl";
        var contentType = new ContentType(MediaTypeNames.Text.Plain);
        var channelName = $"Consumer-Requeue-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        string topicName = $"Consumer-Requeue-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey(topicName);

        _subscription = new GcpSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: new ChannelName(channelName),
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: OnMissingChannel.Create
        );

        _message = new Message(
            new MessageHeader(_myCommand.Id, routingKey, MessageType.MT_COMMAND, correlationId: correlationId,
                replyTo: new RoutingKey(replyTo), contentType: contentType),
            new MessageBody(JsonSerializer.Serialize((object)_myCommand, JsonSerialisationOptions.Options))
        );

        var connection = GatewayFactory.CreateFactory();

        _channelFactory = new GcpPubSubChannelFactory(connection);
        _channel = _channelFactory.CreateAsyncChannel(_subscription);

        _messageProducer = new GcpMessageProducer(connection, 
            new GcpPublication
            {
                Topic = routingKey,
                MakeChannels = OnMissingChannel.Create
            });
    }

    [Fact]
    public async Task When_rejecting_a_message_through_gateway_with_requeue_async()
    {
        await _messageProducer.SendAsync(_message);

        var message = await _channel.ReceiveAsync(TimeSpan.FromMilliseconds(5000));

        await _channel.RejectAsync(message);

        // Let the timeout change
        await Task.Delay(TimeSpan.FromMilliseconds(3000));

        // should requeue_the_message
        message = await _channel.ReceiveAsync(TimeSpan.FromMilliseconds(5000));

        // clear the queue
        await _channel.AcknowledgeAsync(message);

        Assert.Equal(_myCommand.Id, message.Id);
    }

    public async ValueTask DisposeAsync()
    {
        await _channelFactory.DeleteTopicAsync(_subscription);
        await _channelFactory.DeleteSubscriptionAsync(_subscription);
    }
}
