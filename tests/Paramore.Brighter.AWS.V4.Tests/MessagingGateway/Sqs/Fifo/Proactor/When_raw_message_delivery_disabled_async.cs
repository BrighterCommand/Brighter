using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Threading.Tasks;
using Paramore.Brighter.AWS.V4.Tests.Helpers;
using Paramore.Brighter.AWS.V4.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS.V4;
using Xunit;

namespace Paramore.Brighter.AWS.V4.Tests.MessagingGateway.Sqs.Fifo.Proactor;

[Trait("Category", "AWS")]
[Trait("Fragile", "CI")]
public class SqsRawMessageDeliveryTestsAsync : IAsyncDisposable, IDisposable
{
    private readonly SqsMessageProducer _messageProducer;
    private readonly ChannelFactory _channelFactory;
    private readonly IAmAChannelAsync _channel;
    private readonly RoutingKey _routingKey;

    public SqsRawMessageDeliveryTestsAsync()
    {
        var awsConnection = GatewayFactory.CreateFactory();

        _channelFactory = new ChannelFactory(awsConnection);
        var queueName = $"Raw-Msg-Delivery-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        _routingKey = new RoutingKey(queueName);

        const int bufferSize = 10;

        // Set rawMessageDelivery to false
        var queueAttributes = new SqsAttributes(
            rawMessageDelivery: true,
            type: SqsType.Fifo);
        var channelName = new ChannelName(queueName);
        
        _channel = _channelFactory.CreateAsyncChannel(new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(queueName),
            channelName: channelName,
            channelType: ChannelType.PointToPoint,
            routingKey: _routingKey,
            bufferSize: bufferSize,
            messagePumpType: MessagePumpType.Proactor,
            queueAttributes: queueAttributes, 
            makeChannels: OnMissingChannel.Create)
        );

        _messageProducer = new SqsMessageProducer(
            awsConnection,
            new SqsPublication(
                channelName: channelName, 
                queueAttributes: queueAttributes,  
                makeChannels: OnMissingChannel.Create)
            );
    }

    [Fact]
    public async Task When_raw_message_delivery_disabled_async()
    {
        // Arrange
        var messageGroupId = $"MessageGroupId{Guid.NewGuid():N}";
        var deduplicationId = $"DeduplicationId{Guid.NewGuid():N}";
        var messageHeader = new MessageHeader(
            Guid.NewGuid().ToString(),
            _routingKey,
            MessageType.MT_COMMAND,
            correlationId: Guid.NewGuid().ToString(),
            replyTo: RoutingKey.Empty,
            contentType: new ContentType(MediaTypeNames.Text.Plain),
            partitionKey: messageGroupId) { Bag = { [HeaderNames.DeduplicationId] = deduplicationId } };

        var customHeaderItem = new KeyValuePair<string, object>("custom-header-item", "custom-header-item-value");
        messageHeader.Bag.Add(customHeaderItem.Key, customHeaderItem.Value);

        var messageToSend = new Message(messageHeader, new MessageBody("test content one"));

        // Act
        await _messageProducer.SendAsync(messageToSend);

        var messageReceived = await _channel.ReceiveAsync(TimeSpan.FromMilliseconds(10000));

        await _channel.AcknowledgeAsync(messageReceived);

        // Assert
        Assert.Equal(messageToSend.Id, messageReceived.Id);
        Assert.Equal(messageToSend.Header.Topic.ToValidSNSTopicName(true), messageReceived.Header.Topic);
        Assert.Equal(messageToSend.Header.MessageType, messageReceived.Header.MessageType);
        Assert.Equal(messageToSend.Header.CorrelationId, messageReceived.Header.CorrelationId);
        Assert.Equal(messageToSend.Header.ReplyTo, messageReceived.Header.ReplyTo);
        Assert.StartsWith(messageToSend.Header.ContentType?.ToString(), messageReceived.Header.ContentType?.ToString());
        Assert.Contains(customHeaderItem.Key, messageReceived.Header.Bag);
        Assert.Equal(customHeaderItem.Value, messageReceived.Header.Bag[customHeaderItem.Key]);
        Assert.Equal(messageToSend.Body.Value, messageReceived.Body.Value);
        Assert.Equal(messageGroupId, messageReceived.Header.PartitionKey);
        Assert.Contains(HeaderNames.DeduplicationId, messageReceived.Header.Bag);
        Assert.Equal(deduplicationId, messageReceived.Header.Bag[HeaderNames.DeduplicationId]);
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
