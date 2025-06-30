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
public class MessageProducerRequeueTestsAsync : IAsyncDisposable
{
    private readonly IAmAMessageProducerAsync _sender;
    private Message? _requeuedMessage;
    private Message? _receivedMessage;
    private readonly IAmAChannelAsync _channel;
    private readonly GcpPubSubChannelFactory _channelFactory;
    private readonly GcpSubscription<MyCommand> _subscription;
    private readonly Message _message;

    public MessageProducerRequeueTestsAsync()
    {
        const string replyTo = "http:\\queueUrl";
        MyCommand myCommand = new() { Value = "Test" };
        var correlationId = Guid.NewGuid().ToString();
        var contentType = new ContentType(MediaTypeNames.Text.Plain);
        var channelName = $"Producer-Requeue-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        string topicName = $"Producer-Requeue-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey(topicName);

        _subscription = new GcpSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: new ChannelName(channelName),
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: OnMissingChannel.Create
        );

        _message = new Message(
            new MessageHeader(myCommand.Id, routingKey, MessageType.MT_COMMAND, correlationId: correlationId,
                replyTo: new RoutingKey(replyTo), contentType: contentType),
            new MessageBody(JsonSerializer.Serialize((object)myCommand, JsonSerialisationOptions.Options))
        );

        var connection = GatewayFactory.CreateFactory();

        _sender = new GcpMessageProducer(connection, 
            new GcpPublication
            {
                Topic = routingKey,
                MakeChannels = OnMissingChannel.Create
            });

        _channelFactory = new GcpPubSubChannelFactory(connection);
        _channel = _channelFactory.CreateAsyncChannel(_subscription);
    }

    [Fact]
    public async Task When_requeueing_a_message_async()
    {
        await _sender.SendAsync(_message);
        _receivedMessage = await _channel.ReceiveAsync(TimeSpan.FromMilliseconds(5000));
        await _channel.RequeueAsync(_receivedMessage);

        _requeuedMessage = await _channel.ReceiveAsync(TimeSpan.FromMilliseconds(5000));

        await _channel.AcknowledgeAsync(_requeuedMessage);

        Assert.Equal(_receivedMessage.Body.Value, _requeuedMessage.Body.Value);
    }

    public async ValueTask DisposeAsync()
    {
        await _channelFactory.DeleteTopicAsync(_subscription);
        await _channelFactory.DeleteSubscriptionAsync(_subscription);
    }
}
