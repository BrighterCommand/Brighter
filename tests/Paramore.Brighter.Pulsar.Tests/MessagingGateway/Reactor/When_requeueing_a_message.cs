using System.Net.Mime;
using System.Text.Json;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.Pulsar;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Pulsar.Tests.TestDoubles;
using Paramore.Brighter.Pulsar.Tests.Utils;
using Xunit;

namespace Paramore.Brighter.Pulsar.Tests.MessagingGateway.Reactor;

[Trait("Category", "Pulsar")]
public class MessageProducerRequeueTests
{
    private readonly IAmAMessageProducerSync _sender;
    private Message? _requeuedMessage;
    private Message? _receivedMessage;
    private readonly IAmAChannelSync _channel;
    private readonly Message _message;

    public MessageProducerRequeueTests()
    {
        const string replyTo = "http:\\queueUrl";
        var myCommand = new MyCommand { Value = "Test" };
        string correlationId = Guid.NewGuid().ToString();
        var contentType = new ContentType(MediaTypeNames.Text.Plain);
        var channelName = Guid.NewGuid().ToString();
        var routingKey = new RoutingKey("rmq_requeueing");

        var subscription = new PulsarSubscription<MyCommand>(
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

        var connection = GatewayFactory.CreateConnection();

        var channelFactory = new PulsarChannelFactory(new PulsarMessageConsumerFactory(connection));
        var publication = new PulsarPublication { Topic = routingKey };
        _sender = new PulsarMessageProducer(GatewayFactory.CreateProducer(connection, publication), publication,
            TimeProvider.System, InstrumentationOptions.None);
        _channel = channelFactory.CreateSyncChannel(subscription);
    }

    [Fact]
    public void When_requeueing_a_message_async()
    {
        _channel.Purge();
        _sender.Send(_message);
        _receivedMessage = _channel.Receive(TimeSpan.FromMilliseconds(5000));
        _channel.Requeue(_receivedMessage);

        _requeuedMessage = _channel.Receive(TimeSpan.FromMilliseconds(5000));

        _channel.Acknowledge(_requeuedMessage);

        Assert.Equal(_receivedMessage.Body.Value, _requeuedMessage.Body.Value);
    }
}
