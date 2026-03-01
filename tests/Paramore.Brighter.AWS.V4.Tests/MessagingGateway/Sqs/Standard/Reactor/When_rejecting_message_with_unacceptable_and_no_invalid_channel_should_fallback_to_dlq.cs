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
using Paramore.Brighter.AWS.V4.Tests.Helpers;
using Paramore.Brighter.AWS.V4.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.AWSSQS.V4;
using Xunit;

namespace Paramore.Brighter.AWS.V4.Tests.MessagingGateway.Sqs.Standard.Reactor;

[Trait("Category", "AWS")]
[Trait("Fragile", "CI")]
public class SqsMessageConsumerUnacceptableFallbackToDlqTests : IDisposable, IAsyncDisposable
{
    private readonly Message _message;
    private readonly IAmAChannelSync _channel;
    private readonly SqsMessageProducer _messageProducer;
    private readonly ChannelFactory _channelFactory;
    private readonly ChannelFactory _dlqChannelFactory;
    private readonly IAmAChannelSync _dlqChannel;

    public SqsMessageConsumerUnacceptableFallbackToDlqTests()
    {
        var myCommand = new MyCommand { Value = "Test" };
        const string replyTo = "http:\\queueUrl";
        var contentType = new ContentType(MediaTypeNames.Text.Plain);
        var correlationId = Guid.NewGuid().ToString();
        var subscriptionName = $"Consumer-Fallback-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var queueName = $"Consumer-Fallback-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var dlqQueueName = $"Consumer-Fallback-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey(queueName);
        var channelName = new ChannelName(queueName);
        var dlqRoutingKey = new RoutingKey(dlqQueueName);
        var dlqChannelName = new ChannelName(dlqQueueName);

        // Only deadLetterRoutingKey — no invalidMessageRoutingKey
        var subscription = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(subscriptionName),
            channelName: channelName,
            channelType: ChannelType.PointToPoint,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Create,
            deadLetterRoutingKey: dlqRoutingKey);

        _message = new Message(
            new MessageHeader(myCommand.Id, routingKey, MessageType.MT_COMMAND, correlationId: correlationId,
                replyTo: new RoutingKey(replyTo), contentType: contentType),
            new MessageBody(JsonSerializer.Serialize((object)myCommand, JsonSerialisationOptions.Options))
        );

        var awsConnection = GatewayFactory.CreateFactory();

        _channelFactory = new ChannelFactory(awsConnection);
        _channel = _channelFactory.CreateSyncChannel(subscription);

        _messageProducer = new SqsMessageProducer(
            awsConnection,
            new SqsPublication(channelName: channelName, makeChannels: OnMissingChannel.Create));

        // Create a separate channel to consume from the DLQ queue
        var dlqSubscription = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName($"DLQ-Reader-{Guid.NewGuid().ToString()}".Truncate(45)),
            channelName: dlqChannelName,
            channelType: ChannelType.PointToPoint,
            routingKey: dlqRoutingKey,
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Create);

        _dlqChannelFactory = new ChannelFactory(awsConnection);
        _dlqChannel = _dlqChannelFactory.CreateSyncChannel(dlqSubscription);
    }

    [Fact]
    public void When_rejecting_message_with_unacceptable_and_no_invalid_channel_should_fallback_to_dlq()
    {
        //Arrange
        _messageProducer.Send(_message);
        var message = _channel.Receive(TimeSpan.FromMilliseconds(5000));

        //Act
        var originalTopic = message.Header.Topic.Value;
        _channel.Reject(message, new MessageRejectionReason(RejectionReason.Unacceptable, "Test unacceptable message"));

        //Assert - message should fall back to the DLQ since no invalid message queue is configured
        var dlqMessage = _dlqChannel.Receive(TimeSpan.FromMilliseconds(5000));

        Assert.NotEqual(MessageType.MT_NONE, dlqMessage.Header.MessageType);
        Assert.Equal(_message.Body.Value, dlqMessage.Body.Value);

        //verify rejection metadata was added (keys are camelCase due to JSON serialization policy)
        Assert.True(dlqMessage.Header.Bag.ContainsKey("originalTopic"));
        Assert.Equal(originalTopic, dlqMessage.Header.Bag["originalTopic"].ToString());
        Assert.True(dlqMessage.Header.Bag.ContainsKey("rejectionReason"));
        Assert.Equal(RejectionReason.Unacceptable.ToString(), dlqMessage.Header.Bag["rejectionReason"].ToString());
        Assert.True(dlqMessage.Header.Bag.ContainsKey("rejectionTimestamp"));
        Assert.True(dlqMessage.Header.Bag.ContainsKey("originalMessageType"));
        Assert.Equal(MessageType.MT_COMMAND.ToString(), dlqMessage.Header.Bag["originalMessageType"].ToString());

        //verify original message is deleted from source queue
        var sourceMessage = _channel.Receive(TimeSpan.FromMilliseconds(5000));
        Assert.Equal(MessageType.MT_NONE, sourceMessage.Header.MessageType);
    }

    public void Dispose()
    {
        _channelFactory.DeleteTopicAsync().Wait();
        _channelFactory.DeleteQueueAsync().Wait();
        _dlqChannelFactory.DeleteTopicAsync().Wait();
        _dlqChannelFactory.DeleteQueueAsync().Wait();
    }

    public async ValueTask DisposeAsync()
    {
        await _channelFactory.DeleteTopicAsync();
        await _channelFactory.DeleteQueueAsync();
        await _dlqChannelFactory.DeleteTopicAsync();
        await _dlqChannelFactory.DeleteQueueAsync();
    }
}
