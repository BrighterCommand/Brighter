using System;
using System.Net.Mime;
using System.Text.Json;
using Paramore.Brighter.Gcp.Tests.Helper;
using Paramore.Brighter.Gcp.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.GcpPubSub;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Pull.Reactor;

[Trait("Category", "GCP")]
public class MessageProducerRequeueTestsAsync : IDisposable
{
    private readonly IAmAMessageProducerSync _sender;
    private Message? _requeuedMessage;
    private Message? _receivedMessage;
    private readonly IAmAChannelSync _channel;
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
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Create,
            subscriptionMode: SubscriptionMode.Pull
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
        _channel = _channelFactory.CreateSyncChannel(_subscription);
    }

    [Fact]
    public void When_requeueing_a_message()
    {
        _sender.Send(_message);
        _receivedMessage = _channel.Receive(TimeSpan.FromMilliseconds(5000));
        _channel.Requeue(_receivedMessage);

        _requeuedMessage = _channel.Receive(TimeSpan.FromMilliseconds(5000));

        _channel.Acknowledge(_requeuedMessage);

        Assert.Equal(_receivedMessage.Body.Value, _requeuedMessage.Body.Value);
    }

    public void Dispose()
    {
        _channelFactory.DeleteTopic(_subscription);
        _channelFactory.DeleteSubscription(_subscription);
    }
}
