using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Sqs.Standard.Reactor;

[Trait("Category", "AWS")] 
[Trait("Fragile", "CI")]
public class SqsRawMessageDeliveryTests : IDisposable, IAsyncDisposable
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
            name: new SubscriptionName(channelName),
            channelName:new ChannelName(channelName),
            routingKey:_routingKey,
            bufferSize: bufferSize,
            makeChannels: OnMissingChannel.Create,
            messagePumpType: MessagePumpType.Reactor,
            rawMessageDelivery: false));

        _messageProducer = new SnsMessageProducer(awsConnection, 
            new SnsPublication
            {
                MakeChannels = OnMissingChannel.Create 
            });
    }

    [Fact]
    public void When_raw_message_delivery_disabled()
    {
        //arrange
        var messageHeader = new MessageHeader(
            Guid.NewGuid().ToString(), 
            _routingKey, 
            MessageType.MT_COMMAND, 
            correlationId: Guid.NewGuid().ToString(), 
            replyTo: RoutingKey.Empty, 
            contentType: "text\\plain");

        var customHeaderItem = new KeyValuePair<string, object>("custom-header-item", "custom-header-item-value");
        messageHeader.Bag.Add(customHeaderItem.Key, customHeaderItem.Value);

        var messageToSent = new Message(messageHeader, new MessageBody("test content one"));

        //act
        _messageProducer.Send(messageToSent);

        var messageReceived = _channel.Receive(TimeSpan.FromMilliseconds(10000));

        _channel.Acknowledge(messageReceived);

        //assert
        messageReceived.Id.Should().Be(messageToSent.Id);
        messageReceived.Header.Topic.Should().Be(messageToSent.Header.Topic);
        messageReceived.Header.MessageType.Should().Be(messageToSent.Header.MessageType);
        messageReceived.Header.CorrelationId.Should().Be(messageToSent.Header.CorrelationId);
        messageReceived.Header.ReplyTo.Should().Be(messageToSent.Header.ReplyTo);
        messageReceived.Header.ContentType.Should().Be(messageToSent.Header.ContentType);
        messageReceived.Header.Bag.Should().ContainKey(customHeaderItem.Key).And.ContainValue(customHeaderItem.Value);
        messageReceived.Body.Value.Should().Be(messageToSent.Body.Value);
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
