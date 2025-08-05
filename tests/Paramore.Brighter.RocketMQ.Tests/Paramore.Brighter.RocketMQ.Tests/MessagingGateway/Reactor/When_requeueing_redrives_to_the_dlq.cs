using System.Net.Mime;
using System.Text.Json;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.RocketMQ;
using Paramore.Brighter.RocketMQ.Tests.TestDoubles;
using Paramore.Brighter.RocketMQ.Tests.Utils;
using Xunit;

namespace Paramore.Brighter.RocketMQ.Tests.MessagingGateway.Reactor;

[Trait("Category", "RocketMQ")]
[Trait("Fragile", "CI")]
public class MessageProducerDlqTests
{
    private readonly RocketMqMessageProducer _sender;
    private readonly IAmAChannelSync _channel;
    private readonly Message _message;

    public MessageProducerDlqTests()
    {
        MyCommand myCommand = new() { Value = "Test" };
        string correlationId = Guid.NewGuid().ToString();
        const string replyTo = "http:\\queueUrl";
        var contentType = new ContentType(MediaTypeNames.Text.Plain);  
        var queueName = Guid.NewGuid().ToString();
        var routingKey = new RoutingKey("rmq_dead_letter");
        var channelName = new ChannelName(queueName);
        
        var subscription = new RocketMqSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(queueName),
            channelName: channelName,
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
        var publication = new RocketMqPublication { Topic = routingKey };
        _sender = new RocketMqMessageProducer(connection,  
            GatewayFactory.CreateProducer(connection, publication).GetAwaiter().GetResult(),
            publication);

        RocketMqChannelFactory channelFactory = new(new RocketMessageConsumerFactory(connection));
        _channel = channelFactory.CreateSyncChannel(subscription);
    }

    [Fact]
    public void When_requeueing_redrives_to_the_queue()
    {
        _channel.Purge();
        _sender.Send(_message);
        Message receivedMessage;
        for (var i = 0; i < 32; i++)
        {
            receivedMessage = _channel.Receive(TimeSpan.FromSeconds(5000));
            if (receivedMessage.Header.MessageType != MessageType.MT_NONE)
            {
                break;
            }
            
            _channel.Requeue(receivedMessage);
        }

        receivedMessage = _channel.Receive(TimeSpan.FromMilliseconds(5000));
        Assert.Equal(MessageType.MT_NONE, receivedMessage.Header.MessageType);
    }
}
