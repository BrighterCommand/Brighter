using System.Net.Mime;
using System.Text.Json;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.RocketMQ;
using Paramore.Brighter.RocketMQ.Tests.TestDoubles;
using Paramore.Brighter.RocketMQ.Tests.Utils;
using Xunit;

namespace Paramore.Brighter.RocketMQ.Tests.MessagingGateway.Proactor;

[Trait("Category", "RocketMQ")]
[Trait("Fragile", "CI")]
public class MessageProducerDlqTestsAsync
{
    private readonly RocketMqMessageProducer _sender;
    private readonly IAmAChannelAsync _channel;
    private readonly Message _message;

    public MessageProducerDlqTestsAsync()
    {
        MyCommand myCommand = new() { Value = "Test" };
        string correlationId = Guid.NewGuid().ToString();
        const string replyTo = "http:\\queueUrl";
        var contentType = new ContentType(MediaTypeNames.Text.Plain);  
        var queueName = Guid.NewGuid().ToString();
        var routingKey = new RoutingKey("rmq_dead_letter_async");
        var channelName = new ChannelName(queueName);
        
        var subscription = new RocketMqSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(queueName),
            channelName: channelName,
            routingKey: routingKey,
            consumerGroup: Guid.NewGuid().ToString(),
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: OnMissingChannel.Create,
            receiveMessageTimeout: TimeSpan.FromSeconds(1)
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
        _channel = channelFactory.CreateAsyncChannel(subscription);
    }

    [Fact]
    public async Task When_requeueing_redrives_to_the_queue_async()
    {
        await _channel.PurgeAsync();
        await _sender.SendAsync(_message);
        Message receivedMessage;
        for (var i = 0; i < 32; i++)
        {
            receivedMessage = await _channel.ReceiveAsync(TimeSpan.FromSeconds(1));
            if (receivedMessage.Header.MessageType != MessageType.MT_NONE)
            {
                break;
            }
            
            await _channel.RequeueAsync(receivedMessage);
        }
       
        receivedMessage = await _channel.ReceiveAsync(TimeSpan.FromSeconds(1));
        Assert.Equal(MessageType.MT_NONE, receivedMessage.Header.MessageType);
    }
}
