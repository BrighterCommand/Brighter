using System.Net.Mime;
using System.Text.Json;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.RocketMQ;
using Paramore.Brighter.RocketMQ.Tests.TestDoubles;
using Paramore.Brighter.RocketMQ.Tests.Utils;
using Xunit;

namespace Paramore.Brighter.RocketMQ.Tests.MessagingGateway.Reactor;

[Trait("Category", "RocketMQ")]
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
        MyCommand myCommand = new() { Value = "Test" };
        string correlationId = Guid.NewGuid().ToString();
        var contentType = new ContentType(MediaTypeNames.Text.Plain);
        var channelName = Guid.NewGuid().ToString();
        var routingKey = new RoutingKey("rmq_requeueing");

        var subscription = new RocketMqSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: new ChannelName(channelName),
            routingKey: routingKey,
            consumerGroup: Guid.NewGuid().ToString(),
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: OnMissingChannel.Create
        );

        _message = new Message(
            new MessageHeader(myCommand.Id, routingKey, MessageType.MT_COMMAND, correlationId: correlationId,
                replyTo: new RoutingKey(replyTo), contentType: contentType),
            new MessageBody(JsonSerializer.Serialize((object)myCommand, JsonSerialisationOptions.Options))
        );

        var connection = GatewayFactory.CreateConnection();

        RocketMqChannelFactory channelFactory = new(new RocketMessageConsumerFactory(connection));
        var publication = new RocketMqPublication { Topic = routingKey };
        _sender = new RocketMqMessageProducer(connection, 
            GatewayFactory.CreateProducer(connection, publication).GetAwaiter().GetResult(),
            publication);
        _channel = channelFactory.CreateSyncChannel(subscription);
    }

    [Fact]
    public void When_requeueing_a_message_async()
    {
        _channel.Purge();
        _sender.Send(_message);
        _receivedMessage = _channel.Receive(TimeSpan.FromSeconds(5));
        _channel.Requeue(_receivedMessage);

        for (var i = 0; i < 10; i++)
        {
            _requeuedMessage = _channel.Receive(TimeSpan.FromSeconds(10));
            if (_requeuedMessage.Header.MessageType == MessageType.MT_NONE)
            {
                Thread.Sleep(1_000);
                continue;
            }
            
            _channel.Acknowledge(_requeuedMessage);
            break;
        }

        Assert.Equal(_receivedMessage.Body.Value, _requeuedMessage.Body.Value);
    }
}
