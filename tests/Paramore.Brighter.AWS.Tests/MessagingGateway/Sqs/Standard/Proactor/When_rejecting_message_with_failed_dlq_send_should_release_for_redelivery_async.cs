#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Linq;
using System.Net.Mime;
using System.Text.Json;
using System.Threading.Tasks;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Sqs.Standard.Proactor;

/// <summary>
/// Pins issue #4415: when the routing send to a configured DLQ fails, the source message must be
/// released for redelivery (not deleted), matching spec 0037 R-19's GCP behaviour.
/// </summary>
[Trait("Category", "AWS")]
public class SqsMessageConsumerFailedDlqSendReleasesSourceTestsAsync : IDisposable, IAsyncDisposable
{
    private readonly Message _message;
    private readonly SqsMessageConsumer _consumer;
    private readonly SqsMessageProducer _messageProducer;
    private readonly ChannelFactory _channelFactory;

    public SqsMessageConsumerFailedDlqSendReleasesSourceTestsAsync()
    {
        var myCommand = new MyCommand { Value = "Test" };
        const string replyTo = "http:\\queueUrl";
        var contentType = new ContentType(MediaTypeNames.Text.Plain);
        var correlationId = Guid.NewGuid().ToString();
        var subscriptionName = $"Reject-Release-Async-{Guid.NewGuid().ToString()}".Truncate(45);
        var queueName = $"Reject-Release-Async-{Guid.NewGuid().ToString()}".Truncate(45);
        // Never created — the DLQ send must fail because this queue does not exist.
        var dlqQueueName = $"Reject-Release-DLQ-A-{Guid.NewGuid().ToString()}".Truncate(45);
        var routingKey = new RoutingKey(queueName);
        var channelName = new ChannelName(queueName);
        var dlqRoutingKey = new RoutingKey(dlqQueueName);

        var subscription = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(subscriptionName),
            channelName: channelName,
            channelType: ChannelType.PointToPoint,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: OnMissingChannel.Create);

        _message = new Message(
            new MessageHeader(myCommand.Id, routingKey, MessageType.MT_COMMAND, correlationId: correlationId,
                replyTo: new RoutingKey(replyTo), contentType: contentType),
            new MessageBody(JsonSerializer.Serialize((object)myCommand, JsonSerialisationOptions.Options))
        );

        var awsConnection = GatewayFactory.CreateFactory();

        // Create the source queue only; the DLQ is deliberately never created.
        _channelFactory = new ChannelFactory(awsConnection);
        _channelFactory.CreateSyncChannel(subscription);

        _messageProducer = new SqsMessageProducer(
            awsConnection,
            new SqsPublication(channelName: channelName, makeChannels: OnMissingChannel.Create));

        // Built directly (not via the subscription/ChannelFactory) so the DLQ producer uses
        // Validate — it must fail against the DLQ, not silently create it, to exercise the
        // failed-send branch under test.
        _consumer = new SqsMessageConsumer(
            awsConnection,
            channelName.Value,
            deadLetterRoutingKey: dlqRoutingKey,
            makeChannels: OnMissingChannel.Validate);
    }

    [Fact]
    public async Task When_rejecting_message_with_failed_dlq_send_should_release_for_redelivery_async()
    {
        //Arrange
        await _messageProducer.SendAsync(_message);
        var message = (await _consumer.ReceiveAsync(TimeSpan.FromMilliseconds(5000))).First();

        //Act — the DLQ send fails because the DLQ queue does not exist (OnMissingChannel.Validate)
        var result = await _consumer.RejectAsync(message, new MessageRejectionReason(RejectionReason.DeliveryError, "Test delivery error"));

        //Assert — settled by this call, but released for redelivery, not deleted
        Assert.True(result);

        var redelivered = (await _consumer.ReceiveAsync(TimeSpan.FromMilliseconds(5000))).First();
        Assert.NotEqual(MessageType.MT_NONE, redelivered.Header.MessageType);
        Assert.Equal(_message.Body.Value, redelivered.Body.Value);
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
