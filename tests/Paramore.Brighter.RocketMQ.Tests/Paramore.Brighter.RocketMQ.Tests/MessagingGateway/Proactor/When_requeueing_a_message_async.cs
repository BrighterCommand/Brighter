using System.Net.Mime;
using System.Text.Json;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.RocketMQ;
using Paramore.Brighter.RocketMQ.Tests.TestDoubles;
using Paramore.Brighter.RocketMQ.Tests.Utils;
using Xunit;

namespace Paramore.Brighter.RocketMQ.Tests.MessagingGateway.Proactor;

[Trait("Category", "RocketMQ")]
public class MessageProducerRequeueTestsAsync
{
    private readonly IAmAMessageProducerAsync _sender;
    private Message? _requeuedMessage;
    private Message? _receivedMessage;
    private readonly IAmAChannelAsync _channel;
    private readonly Message _message;

    public MessageProducerRequeueTestsAsync()
    {
        const string replyTo = "http:\\queueUrl";
        MyCommand myCommand = new() { Value = "Test" };
        string correlationId = Guid.NewGuid().ToString();
        var contentType = new ContentType(MediaTypeNames.Text.Plain);
        var channelName = Guid.NewGuid().ToString();
        var routingKey = new RoutingKey("rmq_requeueing_async");

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
        _channel = channelFactory.CreateAsyncChannel(subscription);
    }

    [Fact]
    public async Task When_requeueing_a_message_async()
    {
        await _channel.PurgeAsync();
        await _sender.SendAsync(_message);
        _receivedMessage = await _channel.ReceiveAsync(TimeSpan.FromSeconds(5));
        await _channel.RequeueAsync(_receivedMessage);

        for (var i = 0; i < 10; i++)
        {
            _requeuedMessage = await _channel.ReceiveAsync(TimeSpan.FromSeconds(10));
            if (_requeuedMessage.Header.MessageType == MessageType.MT_NONE)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                continue;
            }
            
            await _channel.AcknowledgeAsync(_requeuedMessage);
            break;
        }

        Assert.Equal(_receivedMessage.Body.Value, _requeuedMessage.Body.Value);
    }
}
