#region Licence
/* The MIT License (MIT)
Copyright © 2025 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Net.Mime;
using System.Text.Json;
using System.Threading.Tasks;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.AWSSQS;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Sqs.Fifo.Reactor;

[Category("AWS")]
[Property("Fragile", "CI")]
public class SqsMessageConsumerFifoDeliveryErrorDlqTests : IAsyncDisposable
{
    private readonly Message _message;
    private readonly IAmAChannelSync _channel;
    private readonly SqsMessageProducer _messageProducer;
    private readonly ChannelFactory _channelFactory;
    private readonly ChannelFactory _dlqChannelFactory;
    private readonly IAmAChannelSync _dlqChannel;
    private readonly string _messageGroupId;
    private readonly string _deduplicationId;

    public SqsMessageConsumerFifoDeliveryErrorDlqTests()
    {
        var myCommand = new MyCommand { Value = "Test" };
        const string replyTo = "http:\\queueUrl";
        var contentType = new ContentType(MediaTypeNames.Text.Plain);
        var correlationId = Guid.NewGuid().ToString();
        var subscriptionName = $"Consumer-DLQ-Fifo-{Guid.NewGuid().ToString()}".Truncate(45);
        var queueName = $"Consumer-DLQ-Fifo-{Guid.NewGuid().ToString()}".Truncate(45);
        var dlqQueueName = $"Consumer-DLQ-Fifo-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey(queueName);
        var channelName = new ChannelName(queueName);
        var dlqRoutingKey = new RoutingKey(dlqQueueName);
        var dlqChannelName = new ChannelName(dlqQueueName);

        _messageGroupId = $"MessageGroup{Guid.NewGuid():N}";
        _deduplicationId = $"DeduplicationId{Guid.NewGuid():N}";

        var queueAttributes = new SqsAttributes(type: SqsType.Fifo, rawMessageDelivery: true);

        var subscription = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(subscriptionName),
            channelName: channelName,
            channelType: ChannelType.PointToPoint,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Reactor,
            queueAttributes: queueAttributes,
            makeChannels: OnMissingChannel.Create,
            deadLetterRoutingKey: dlqRoutingKey);

        _message = new Message(
            new MessageHeader(myCommand.Id, routingKey, MessageType.MT_COMMAND, correlationId: correlationId,
                replyTo: new RoutingKey(replyTo), contentType: contentType, partitionKey: _messageGroupId)
            {
                Bag = { [HeaderNames.DeduplicationId] = _deduplicationId }
            },
            new MessageBody(JsonSerializer.Serialize((object)myCommand, JsonSerialisationOptions.Options))
        );

        var awsConnection = GatewayFactory.CreateFactory();

        _channelFactory = new ChannelFactory(awsConnection);
        _channel = _channelFactory.CreateSyncChannel(subscription);

        _messageProducer = new SqsMessageProducer(
            awsConnection,
            new SqsPublication(channelName: channelName, makeChannels: OnMissingChannel.Create, queueAttributes: queueAttributes));

        var dlqSubscription = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName($"DLQ-Reader-{Guid.NewGuid().ToString()}".Truncate(45)),
            channelName: dlqChannelName,
            channelType: ChannelType.PointToPoint,
            routingKey: dlqRoutingKey,
            messagePumpType: MessagePumpType.Reactor,
            queueAttributes: queueAttributes,
            makeChannels: OnMissingChannel.Create);

        _dlqChannelFactory = new ChannelFactory(awsConnection);
        _dlqChannel = _dlqChannelFactory.CreateSyncChannel(dlqSubscription);
    }

    [Test]
    public async Task When_rejecting_fifo_message_with_delivery_error_should_send_to_dlq()
    {
        //Arrange
        await _messageProducer.SendAsync(_message);
        var message = _channel.Receive(TimeSpan.FromMilliseconds(5000));

        //Act
        var originalTopic = message.Header.Topic.Value;
        _channel.Reject(message, new MessageRejectionReason(RejectionReason.DeliveryError, "Test delivery error"));

        //Assert - message should appear on DLQ
        var dlqMessage = _dlqChannel.Receive(TimeSpan.FromMilliseconds(5000));

        await Assert.That(dlqMessage.Header.MessageType).IsNotEqualTo(MessageType.MT_NONE);
        await Assert.That(dlqMessage.Body.Value).IsEqualTo(_message.Body.Value);

        //verify FIFO attributes are preserved on DLQ message
        await Assert.That(dlqMessage.Header.PartitionKey).IsEqualTo(_messageGroupId);

        //verify rejection metadata was added
        await Assert.That(dlqMessage.Header.Bag.ContainsKey("originalTopic")).IsTrue();
        await Assert.That(dlqMessage.Header.Bag["originalTopic"].ToString()).IsEqualTo(originalTopic);
        await Assert.That(dlqMessage.Header.Bag.ContainsKey("rejectionReason")).IsTrue();
        await Assert.That(dlqMessage.Header.Bag["rejectionReason"].ToString()).IsEqualTo(RejectionReason.DeliveryError.ToString());
        await Assert.That(dlqMessage.Header.Bag.ContainsKey("rejectionTimestamp")).IsTrue();
        await Assert.That(dlqMessage.Header.Bag.ContainsKey("originalMessageType")).IsTrue();
        await Assert.That(dlqMessage.Header.Bag["originalMessageType"].ToString()).IsEqualTo(MessageType.MT_COMMAND.ToString());

        //verify original message is deleted from source queue
        var sourceMessage = _channel.Receive(TimeSpan.FromMilliseconds(5000));
        await Assert.That(sourceMessage.Header.MessageType).IsEqualTo(MessageType.MT_NONE);
    }

    [After(Test)]
    public async Task Cleanup()
    {
        await _channelFactory.DeleteTopicAsync();
        await _channelFactory.DeleteQueueAsync();
        await _dlqChannelFactory.DeleteTopicAsync();
        await _dlqChannelFactory.DeleteQueueAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _channelFactory.DeleteTopicAsync();
        await _channelFactory.DeleteQueueAsync();
        await _dlqChannelFactory.DeleteTopicAsync();
        await _dlqChannelFactory.DeleteQueueAsync();
    }
}
