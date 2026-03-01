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
public class SqsMessageConsumerUnacceptableInvalidChannelTests : IDisposable, IAsyncDisposable
{
    private readonly Message _message;
    private readonly IAmAChannelSync _channel;
    private readonly SqsMessageProducer _messageProducer;
    private readonly ChannelFactory _channelFactory;
    private readonly ChannelFactory _invalidChannelFactory;
    private readonly ChannelFactory _dlqChannelFactory;
    private readonly IAmAChannelSync _invalidChannel;
    private readonly IAmAChannelSync _dlqChannel;

    public SqsMessageConsumerUnacceptableInvalidChannelTests()
    {
        var myCommand = new MyCommand { Value = "Test" };
        const string replyTo = "http:\\queueUrl";
        var contentType = new ContentType(MediaTypeNames.Text.Plain);
        var correlationId = Guid.NewGuid().ToString();
        var subscriptionName = $"Consumer-Invalid-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var queueName = $"Consumer-Invalid-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var invalidQueueName = $"Consumer-Invalid-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var dlqQueueName = $"Consumer-Invalid-Tests-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey(queueName);
        var channelName = new ChannelName(queueName);
        var invalidRoutingKey = new RoutingKey(invalidQueueName);
        var invalidChannelName = new ChannelName(invalidQueueName);
        var dlqRoutingKey = new RoutingKey(dlqQueueName);
        var dlqChannelName = new ChannelName(dlqQueueName);

        var subscription = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(subscriptionName),
            channelName: channelName,
            channelType: ChannelType.PointToPoint,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Create,
            deadLetterRoutingKey: dlqRoutingKey,
            invalidMessageRoutingKey: invalidRoutingKey);

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

        // Create a separate channel to consume from the invalid message queue
        var invalidSubscription = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName($"Invalid-Reader-{Guid.NewGuid().ToString()}".Truncate(45)),
            channelName: invalidChannelName,
            channelType: ChannelType.PointToPoint,
            routingKey: invalidRoutingKey,
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Create);

        _invalidChannelFactory = new ChannelFactory(awsConnection);
        _invalidChannel = _invalidChannelFactory.CreateSyncChannel(invalidSubscription);

        // Create a separate channel to consume from the DLQ queue (to verify it stays empty)
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
    public void When_rejecting_message_with_unacceptable_reason_should_send_to_invalid_channel()
    {
        //Arrange
        _messageProducer.Send(_message);
        var message = _channel.Receive(TimeSpan.FromMilliseconds(5000));

        //Act
        var originalTopic = message.Header.Topic.Value;
        _channel.Reject(message, new MessageRejectionReason(RejectionReason.Unacceptable, "Test unacceptable message"));

        //Assert - message should appear on the invalid message queue
        var invalidMessage = _invalidChannel.Receive(TimeSpan.FromMilliseconds(5000));

        Assert.NotEqual(MessageType.MT_NONE, invalidMessage.Header.MessageType);
        Assert.Equal(_message.Body.Value, invalidMessage.Body.Value);

        //verify rejection metadata was added (keys are camelCase due to JSON serialization policy)
        Assert.True(invalidMessage.Header.Bag.ContainsKey("originalTopic"));
        Assert.Equal(originalTopic, invalidMessage.Header.Bag["originalTopic"].ToString());
        Assert.True(invalidMessage.Header.Bag.ContainsKey("rejectionReason"));
        Assert.Equal(RejectionReason.Unacceptable.ToString(), invalidMessage.Header.Bag["rejectionReason"].ToString());
        Assert.True(invalidMessage.Header.Bag.ContainsKey("rejectionTimestamp"));
        Assert.True(invalidMessage.Header.Bag.ContainsKey("originalMessageType"));
        Assert.Equal(MessageType.MT_COMMAND.ToString(), invalidMessage.Header.Bag["originalMessageType"].ToString());

        //verify message did NOT go to the DLQ
        var dlqMessage = _dlqChannel.Receive(TimeSpan.FromMilliseconds(5000));
        Assert.Equal(MessageType.MT_NONE, dlqMessage.Header.MessageType);

        //verify original message is deleted from source queue
        var sourceMessage = _channel.Receive(TimeSpan.FromMilliseconds(5000));
        Assert.Equal(MessageType.MT_NONE, sourceMessage.Header.MessageType);
    }

    public void Dispose()
    {
        _channelFactory.DeleteTopicAsync().Wait();
        _channelFactory.DeleteQueueAsync().Wait();
        _invalidChannelFactory.DeleteTopicAsync().Wait();
        _invalidChannelFactory.DeleteQueueAsync().Wait();
        _dlqChannelFactory.DeleteTopicAsync().Wait();
        _dlqChannelFactory.DeleteQueueAsync().Wait();
    }

    public async ValueTask DisposeAsync()
    {
        await _channelFactory.DeleteTopicAsync();
        await _channelFactory.DeleteQueueAsync();
        await _invalidChannelFactory.DeleteTopicAsync();
        await _invalidChannelFactory.DeleteQueueAsync();
        await _dlqChannelFactory.DeleteTopicAsync();
        await _dlqChannelFactory.DeleteQueueAsync();
    }
}
