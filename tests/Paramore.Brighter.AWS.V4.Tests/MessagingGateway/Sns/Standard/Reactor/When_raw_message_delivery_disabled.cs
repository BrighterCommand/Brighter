using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Threading.Tasks;
using Paramore.Brighter.AWS.V4.Tests.Helpers;
using Paramore.Brighter.AWS.V4.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS.V4;
using Amazon.SimpleNotificationService.Model;

namespace Paramore.Brighter.AWS.V4.Tests.MessagingGateway.Sns.Standard.Reactor;

[Category("AWS")] 
public class SqsRawMessageDeliveryTests : IAsyncDisposable
{
    private readonly SnsMessageProducer _messageProducer;
    private readonly ChannelFactory _channelFactory;
    private readonly IAmAChannelSync _channel;
    private readonly RoutingKey _routingKey;

    public SqsRawMessageDeliveryTests()
    {
        var awsConnection = GatewayFactory.CreateFactory();

        _channelFactory = new ChannelFactory(awsConnection);
        var channelName = $"Raw-Msg-Delivery-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        _routingKey = new RoutingKey($"Raw-Msg-Delivery-Tests-{Guid.NewGuid().ToString()}".Truncate(45));

        var bufferSize = 10;

        //Set rawMessageDelivery to false
        _channel = _channelFactory.CreateSyncChannel(new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: new ChannelName(channelName),
            channelType: ChannelType.PubSub,
            routingKey: _routingKey,
            bufferSize: bufferSize,
            messagePumpType: MessagePumpType.Reactor,
            queueAttributes: new SqsAttributes(
                rawMessageDelivery: false,
                tags: new Dictionary<string, string> { { "Environment", "Test" } }), makeChannels: OnMissingChannel.Create,
            topicAttributes: new SnsAttributes(tags: [new Tag { Key = "Environment", Value = "Test" }])));

        _messageProducer = new SnsMessageProducer(awsConnection, 
            new SnsPublication
            {
                MakeChannels = OnMissingChannel.Create 
            });
    }

    [Test]
    public async Task When_raw_message_delivery_disabled()
    {
        //arrange
        var messageHeader = new MessageHeader(
            Guid.NewGuid().ToString(), 
            _routingKey, 
            MessageType.MT_COMMAND, 
            correlationId: Guid.NewGuid().ToString(), 
            replyTo: RoutingKey.Empty, 
            contentType: new ContentType(MediaTypeNames.Text.Plain));

        var customHeaderItem = new KeyValuePair<string, object>("custom-header-item", "custom-header-item-value");
        messageHeader.Bag.Add(customHeaderItem.Key, customHeaderItem.Value);

        var messageToSent = new Message(messageHeader, new MessageBody("test content one"));

        //act
        await _messageProducer.SendAsync(messageToSent);

        var messageReceived = _channel.Receive(TimeSpan.FromMilliseconds(10000));

        _channel.Acknowledge(messageReceived);

        //assert
        await Assert.That(messageReceived.Id).IsEqualTo(messageToSent.Id);
        await Assert.That(messageReceived.Header.Topic).IsEqualTo(messageToSent.Header.Topic);
        await Assert.That(messageReceived.Header.MessageType).IsEqualTo(messageToSent.Header.MessageType);
        await Assert.That(messageReceived.Header.CorrelationId).IsEqualTo(messageToSent.Header.CorrelationId);
        await Assert.That(messageReceived.Header.ReplyTo).IsEqualTo(messageToSent.Header.ReplyTo);
        await Assert.That(messageReceived.Header.ContentType).IsEqualTo(messageToSent.Header.ContentType);
        await Assert.That(messageReceived.Header.Bag).ContainsKey(customHeaderItem.Key);
        await Assert.That(messageReceived.Header.Bag[customHeaderItem.Key]).IsEqualTo(customHeaderItem.Value);
        await Assert.That(messageReceived.Body.Value).IsEqualTo(messageToSent.Body.Value);
    }

    [After(Test)]
    public async Task Cleanup()
    {
        await _channelFactory.DeleteTopicAsync(); 
        await _channelFactory.DeleteQueueAsync();
    }
        
    public async ValueTask DisposeAsync()
    {
        await _channelFactory.DeleteTopicAsync(); 
        await _channelFactory.DeleteQueueAsync();
    }
}
