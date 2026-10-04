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
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.SimpleNotificationService.Model;
using Paramore.Brighter.AWS.V4.Tests.Helpers;
using Paramore.Brighter.AWS.V4.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.AWSSQS.V4;
using Xunit;

namespace Paramore.Brighter.AWS.V4.Tests.MessagingGateway.Sqs.Standard.Reactor;

/// <summary>
/// V4 lockstep twin of the v3 <c>SqsVisibilityTimeoutLapseDeliveryCountTests</c> (NFR-6, R-12).
/// Verifies that an SQS message whose visibility timeout lapses presents a higher delivery count
/// on the expiry redelivery, with no pump and no Requeue (R-1 expiry path, AC-42).
/// Covers both message creators via the <paramref name="rawMessageDelivery"/> theory parameter:
/// <c>true</c> → <c>SqsMessageCreator</c>; <c>false</c> → <c>SqsInlineMessageCreator</c>.
/// </summary>
[Trait("Category", "AWS")]
public class SqsVisibilityTimeoutLapseDeliveryCountTests : IDisposable, IAsyncDisposable
{
    private readonly AWSMessagingGatewayConnection _awsConnection;
    private readonly ChannelFactory _channelFactory;

    public SqsVisibilityTimeoutLapseDeliveryCountTests()
    {
        _awsConnection = GatewayFactory.CreateFactory();
        _channelFactory = new ChannelFactory(_awsConnection);
    }

    /// <summary>
    /// A message whose visibility timeout lapses presents a strictly greater <c>HandledCount</c>
    /// on the expiry redelivery than it did on the first delivery (R-1, AC-42).
    /// The consumer receives once, does not ack, reject or requeue, waits past the timeout, then
    /// receives the redelivery.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task When_an_sqs_visibility_timeout_lapses_should_present_greater_delivery_count(bool rawMessageDelivery)
    {
        // Arrange
        var topicName = $"Vt-Reactor-{Guid.NewGuid()}".Truncate(45);
        var queueName = $"Vt-Reactor-{Guid.NewGuid()}".Truncate(45);
        var routingKey = new RoutingKey(topicName);
        var channelName = new ChannelName(queueName);

        var subscription = new SqsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(queueName),
            channelName: channelName,
            channelType: ChannelType.PubSub,
            routingKey: routingKey,
            requeueCount: -1,
            messagePumpType: MessagePumpType.Reactor,
            queueAttributes: new SqsAttributes(
                rawMessageDelivery: rawMessageDelivery,
                lockTimeout: TimeSpan.FromSeconds(5)),
            makeChannels: OnMissingChannel.Create,
            topicAttributes: new SnsAttributes(tags: [new Tag { Key = "Environment", Value = "Test" }]));

        // Create the SNS topic and SQS queue; we drive the consumer directly — no pump
        _channelFactory.CreateSyncChannel(subscription);

        var consumer = (IAmAMessageConsumerSync)new SqsMessageConsumerFactory(_awsConnection).Create(subscription);

        var producer = new SnsMessageProducer(_awsConnection, new SnsPublication
        {
            Topic = routingKey,
            RequestType = typeof(MyCommand),
            MakeChannels = OnMissingChannel.Validate
        });

        var cmd = new MyCommand { Value = "visibility timeout test" };
        var message = new Message(
            new MessageHeader(cmd.Id, routingKey, MessageType.MT_COMMAND),
            new MessageBody(JsonSerializer.Serialize((object)cmd, JsonSerialisationOptions.Options)));

        producer.Send(message);

        // Act
        // Receive m1 — do not ack, reject or requeue; let the 5-second visibility timeout lapse
        var m1 = consumer.Receive(TimeSpan.FromSeconds(10))
            .FirstOrDefault(m => !m.IsEmpty);

        Assert.NotNull(m1);

        // Wait past the 5-second visibility timeout so SQS makes the message visible again
        await Task.Delay(TimeSpan.FromSeconds(10));

        // Poll for the expiry redelivery (m2)
        Message? m2 = null;
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(45))
        {
            var received = consumer.Receive(TimeSpan.FromSeconds(1));
            var candidate = received.FirstOrDefault(m => !m.IsEmpty);
            if (candidate != null)
            {
                m2 = candidate;
                break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        // Assert — AC-42: the expiry redelivery presents a count strictly greater than the first delivery
        Assert.NotNull(m2);
        Assert.True(m2.Header.HandledCount > m1.Header.HandledCount,
            $"AC-42: visibility-timeout redelivery m2.HandledCount ({m2.Header.HandledCount}) "
            + $"must be strictly greater than m1.HandledCount ({m1.Header.HandledCount})");

        consumer.Acknowledge(m2);
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
