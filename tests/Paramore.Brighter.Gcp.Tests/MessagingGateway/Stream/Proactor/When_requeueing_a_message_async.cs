using System;
using System.Net.Mime;
using System.Text.Json;
using System.Threading.Tasks;
using Paramore.Brighter.Gcp.Tests.Helper;
using Paramore.Brighter.Gcp.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.GcpPubSub;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Stream.Proactor;

[Trait("Category", "GCP")]
public class MessageProducerRequeueTestsAsync : IDisposable
{
    private readonly IAmAMessageProducerAsync _sender;
    private Message? _requeuedMessage;
    private Message? _receivedMessage;
    private readonly IAmAChannelAsync _channel;
    private readonly GcpPubSubChannelFactory _channelFactory;
    private readonly GcpPubSubSubscription<MyCommand> _pubSubSubscription;
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

        _pubSubSubscription = new GcpPubSubSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: new ChannelName(channelName),
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: OnMissingChannel.Create,
            subscriptionMode: SubscriptionMode.Stream
        );

        _message = new Message(
            new MessageHeader(myCommand.Id, routingKey, MessageType.MT_COMMAND, correlationId: correlationId,
                replyTo: new RoutingKey(replyTo), contentType: contentType),
            new MessageBody(JsonSerializer.Serialize(myCommand, JsonSerialisationOptions.Options))
        );

        _sender = GatewayFactory.CreateProducer(
            new GcpPublication
            {
                Topic = routingKey,
                MakeChannels = OnMissingChannel.Create
            });

        _channelFactory = GatewayFactory.CreateChannelFactory();
        _channel = _channelFactory.CreateAsyncChannel(_pubSubSubscription);
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

    public void Dispose()
    {
        _channelFactory.DeleteTopic(_pubSubSubscription);
        _channelFactory.DeleteSubscription(_pubSubSubscription);
    }
}
