﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Sns.Standard.Proactor;

[Trait("Category", "AWS")]
[Trait("Fragile", "CI")]
public class SqsRawMessageDeliveryTestsAsync : IAsyncDisposable, IDisposable
{
    private readonly SnsMessageProducer _messageProducer;
    private readonly ChannelFactory _channelFactory;
    private readonly IAmAChannelAsync _channel;
    private readonly RoutingKey _routingKey;

    public SqsRawMessageDeliveryTestsAsync()
    {
        var awsConnection = GatewayFactory.CreateFactory();

        _channelFactory = new ChannelFactory(awsConnection);
        var channelName = $"Raw-Msg-Delivery-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        _routingKey = new RoutingKey($"Raw-Msg-Delivery-Tests-{Guid.NewGuid().ToString()}".Truncate(45));

        var bufferSize = 10;

        // Set rawMessageDelivery to false
        _channel = _channelFactory.CreateAsyncChannel(new SqsSubscription<MyCommand>(
            name: new SubscriptionName(channelName),
            channelName: new ChannelName(channelName),
            routingKey: _routingKey,
            bufferSize: bufferSize,
            makeChannels: OnMissingChannel.Create,
            rawMessageDelivery: false));

        _messageProducer = new SnsMessageProducer(awsConnection,
            new SnsPublication
            {
                MakeChannels = OnMissingChannel.Create
            });
    }

    [Fact]
    public async Task When_raw_message_delivery_disabled_async()
    {
        // Arrange
        var messageHeader = new MessageHeader(
            Guid.NewGuid().ToString(),
            _routingKey,
            MessageType.MT_COMMAND,
            correlationId: Guid.NewGuid().ToString(),
            replyTo: RoutingKey.Empty,
            contentType: "text\\plain");

        var customHeaderItem = new KeyValuePair<string, object>("custom-header-item", "custom-header-item-value");
        messageHeader.Bag.Add(customHeaderItem.Key, customHeaderItem.Value);

        var messageToSend = new Message(messageHeader, new MessageBody("test content one"));

        // Act
        await _messageProducer.SendAsync(messageToSend);

        var messageReceived = await _channel.ReceiveAsync(TimeSpan.FromMilliseconds(10000));

        await _channel.AcknowledgeAsync(messageReceived);

        // Assert
        Assert.Equal(messageToSend.Id, messageReceived.Id);
        Assert.Equal(messageToSend.Header.Topic, messageReceived.Header.Topic);
        Assert.Equal(messageToSend.Header.MessageType, messageReceived.Header.MessageType);
        Assert.Equal(messageToSend.Header.CorrelationId, messageReceived.Header.CorrelationId);
        Assert.Equal(messageToSend.Header.ReplyTo, messageReceived.Header.ReplyTo);
        Assert.Equal(messageToSend.Header.ContentType, messageReceived.Header.ContentType);
        Assert.Contains(customHeaderItem.Key, messageReceived.Header.Bag);
        Assert.Equal(customHeaderItem.Value, messageReceived.Header.Bag[customHeaderItem.Key]);
        Assert.Equal(messageToSend.Body.Value, messageReceived.Body.Value);
    }
        
    public void Dispose()
    {
        _channelFactory.DeleteTopicAsync().Wait(); 
        _channelFactory.DeleteQueueAsync().Wait();
    }

    public async ValueTask DisposeAsync()
    {
        await _channelFactory.DeleteTopicAsync();
        await _channelFactory.DeleteQueueAsync();
    }
}
