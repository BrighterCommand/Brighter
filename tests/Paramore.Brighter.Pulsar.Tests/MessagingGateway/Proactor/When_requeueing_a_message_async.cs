using System.Net.Mime;
using System.Text.Json;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.Pulsar;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Pulsar.Tests.TestDoubles;
using Paramore.Brighter.Pulsar.Tests.Utils;
using Xunit;

namespace Paramore.Brighter.Pulsar.Tests.MessagingGateway.Proactor;

[Trait("Category", "Pulsar")]
public class MessageProducerRequeueTestsAsync : IAsyncDisposable
{
    private readonly IAmAMessageProducerAsync _sender;
    private Message? _requeuedMessage;
    private Message? _receivedMessage;
    private readonly IAmAChannelAsync _channel;
    private readonly Message _message;

    public MessageProducerRequeueTestsAsync()
    {
        const string replyTo = "http:\\queueUrl";
        var myCommand = new MyCommand () { Value = "Test" };
        string correlationId = Guid.NewGuid().ToString();
        var contentType = new ContentType(MediaTypeNames.Text.Plain);
        var channelName = Guid.NewGuid().ToString();
        var routingKey = new RoutingKey(Guid.NewGuid().ToString());

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
        _channel = channelFactory.CreateAsyncChannel(subscription);
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
        await _sender.DisposeAsync();
        _channel.Dispose();
    }
}
