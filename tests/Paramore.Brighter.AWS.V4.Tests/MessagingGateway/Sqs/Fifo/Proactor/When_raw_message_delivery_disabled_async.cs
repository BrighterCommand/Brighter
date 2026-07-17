using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Threading.Tasks;
using Paramore.Brighter.AWS.V4.Tests.Helpers;
using Paramore.Brighter.AWS.V4.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS.V4;

namespace Paramore.Brighter.AWS.V4.Tests.MessagingGateway.Sqs.Fifo.Proactor;

[Category("AWS")]
[Property("Fragile", "CI")]
public class SqsRawMessageDeliveryTestsAsync : IAsyncDisposable
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
            type: SqsType.Fifo,
            tags: new Dictionary<string, string> { { "Environment", "Test" } });
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

    [Test]
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
        await Assert.That(messageReceived.Id).IsEqualTo(messageToSend.Id);
        await Assert.That(messageReceived.Header.Topic).IsEqualTo(messageToSend.Header.Topic.ToValidSNSTopicName(true));
        await Assert.That(messageReceived.Header.MessageType).IsEqualTo(messageToSend.Header.MessageType);
        await Assert.That(messageReceived.Header.CorrelationId).IsEqualTo(messageToSend.Header.CorrelationId);
        await Assert.That(messageReceived.Header.ReplyTo).IsEqualTo(messageToSend.Header.ReplyTo);
        await Assert.That(messageReceived.Header.ContentType?.ToString()).StartsWith(messageToSend.Header.ContentType?.ToString());
        await Assert.That(messageReceived.Header.Bag).ContainsKey(customHeaderItem.Key);
        await Assert.That(messageReceived.Header.Bag[customHeaderItem.Key]).IsEqualTo(customHeaderItem.Value);
        await Assert.That(messageReceived.Body.Value).IsEqualTo(messageToSend.Body.Value);
        await Assert.That(messageReceived.Header.PartitionKey).IsEqualTo(messageGroupId);
        await Assert.That(messageReceived.Header.Bag).ContainsKey(HeaderNames.DeduplicationId);
        await Assert.That(messageReceived.Header.Bag[HeaderNames.DeduplicationId]).IsEqualTo(deduplicationId);
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
