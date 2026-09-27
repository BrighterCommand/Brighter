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
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.SimpleNotificationService.Model;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.MessagingGateway.SqsStandard;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Sqs.Standard.Reactor;

/// <summary>
/// Characterises R-8 / AC-8: when both a Brighter budget (<c>requeueCount: 3</c>) and a native
/// SQS redrive limit (<c>RedrivePolicy.maxReceiveCount: 5</c>) are configured on the same
/// subscription, Brighter fires first (budget 3 &lt; native 5) and the message lands on the
/// Brighter DLQ with <c>rejectionReason == "DeliveryError"</c>.  The native redrive target stays
/// empty because Brighter deletes the original before SQS reaches its own threshold.
/// The Brighter DLQ and the native redrive target are DIFFERENT queues.
/// Covers both message creators via <paramref name="rawMessageDelivery"/>:
/// <c>true</c> → <c>SqsMessageCreator</c>; <c>false</c> → <c>SqsInlineMessageCreator</c>.
/// </summary>
[Trait("Category", "AWS")]
[Collection("SqsStandard")]
public class SqsBudgetBelowNativeLimitReactorTests : IDisposable, IAsyncDisposable
{
    private readonly AWSMessagingGatewayConnection _awsConnection;
    private readonly ChannelFactory _channelFactory;
    private ChannelFactory? _dlqChannelFactory;
    private ChannelFactory? _nativeTargetChannelFactory;

    public SqsBudgetBelowNativeLimitReactorTests()
    {
        ConformanceDeferredPump.ResetDispatchCount();
        _awsConnection = GatewayFactory.CreateFactory();
        _channelFactory = new ChannelFactory(_awsConnection);
    }

    /// <summary>
    /// With <c>requeueCount: 3</c> and <c>RedrivePolicy(maxReceiveCount: 5)</c>, the Brighter
    /// budget is exhausted first.  The Brighter DLQ copy carries <c>rejectionReason ==
    /// "DeliveryError"</c> and the native redrive target stays empty (R-8, AC-8).
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task When_sqs_budget_is_below_native_redrive_limit_should_dead_letter_through_brighter(
        bool rawMessageDelivery)
    {
        // Arrange
        var topicName = $"BNL-Reactor-{Guid.NewGuid()}".Truncate(45);
        var queueName = $"BNL-Reactor-{Guid.NewGuid()}".Truncate(45);
        var dlqQueueName = $"BNL-DLQ-{Guid.NewGuid()}".Truncate(45);         // Brighter DLQ
        var nativeTargetQueueName = $"BNL-NTG-{Guid.NewGuid()}".Truncate(45); // SQS native target

        var routingKey = new RoutingKey(topicName);
        var channelName = new ChannelName(queueName);
        var dlqRoutingKey = new RoutingKey(dlqQueueName);
        var dlqChannelName = new ChannelName(dlqQueueName);
        var nativeTargetChannelName = new ChannelName(nativeTargetQueueName);
        var nativeTargetRoutingKey = new RoutingKey(nativeTargetQueueName);

        // Subscription: Brighter budget = 3, native SQS redrive limit = 5 (distinct queue).
        // Both routes are active; Brighter fires first because 3 < 5.
        var subscription = new SqsSubscription<ConformanceDeferredCommand>(
            subscriptionName: new SubscriptionName(queueName),
            channelName: channelName,
            channelType: ChannelType.PubSub,
            routingKey: routingKey,
            requeueCount: 3,
            requeueDelay: TimeSpan.Zero,
            messagePumpType: MessagePumpType.Reactor,
            queueAttributes: new SqsAttributes(
                rawMessageDelivery: rawMessageDelivery,
                lockTimeout: TimeSpan.FromSeconds(5),
                redrivePolicy: new RedrivePolicy(nativeTargetChannelName, 5)),
            makeChannels: OnMissingChannel.Create,
            topicAttributes: new SnsAttributes(tags: [new Tag { Key = "Environment", Value = "Test" }]),
            deadLetterRoutingKey: dlqRoutingKey);

        // Create SNS topic, SQS source queue, and native target queue (via redrivePolicy)
        _channelFactory.CreateSyncChannel(subscription);

        // Create the Brighter DLQ so rejected messages have somewhere to land
        var dlqSubscription = new SqsSubscription<ConformanceDeferredCommand>(
            subscriptionName: new SubscriptionName($"BNL-DLQ-Rdr-{Guid.NewGuid()}".Truncate(45)),
            channelName: dlqChannelName,
            channelType: ChannelType.PointToPoint,
            routingKey: dlqRoutingKey,
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Create);

        _dlqChannelFactory = new ChannelFactory(_awsConnection);
        var dlqChannel = _dlqChannelFactory.CreateSyncChannel(dlqSubscription);

        // Create a reader channel for the native target (already provisioned by redrivePolicy above)
        var nativeTargetSubscription = new SqsSubscription<ConformanceDeferredCommand>(
            subscriptionName: new SubscriptionName($"BNL-NTG-Rdr-{Guid.NewGuid()}".Truncate(45)),
            channelName: nativeTargetChannelName,
            channelType: ChannelType.PointToPoint,
            routingKey: nativeTargetRoutingKey,
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Create);

        _nativeTargetChannelFactory = new ChannelFactory(_awsConnection);
        var nativeTargetChannel = _nativeTargetChannelFactory.CreateSyncChannel(nativeTargetSubscription);

        // Wrap the underlying consumer in a recording decorator so dispatch counts are observable
        var consumer = new SqsMessageConsumerFactory(_awsConnection).Create(subscription);
        var channel = ConformanceDeferredPump.CreateRecordingChannel(channelName, routingKey, consumer);

        var producer = new SnsMessageProducer(_awsConnection, new SnsPublication
        {
            Topic = routingKey,
            RequestType = typeof(ConformanceDeferredCommand),
            MakeChannels = OnMissingChannel.Validate
        });

        var cmd = new ConformanceDeferredCommand { Value = "budget below native limit test" };
        var message = new Message(
            new MessageHeader(cmd.Id, routingKey, MessageType.MT_COMMAND),
            new MessageBody(JsonSerializer.Serialize(cmd, JsonSerialisationOptions.Options)));

        producer.Send(message);

        // Act — run the pump with budget 3; wait until 3 dispatches (budget exhausted, DLQ sent).
        // The bound (90 s) ensures the mutated run (which never rejects) does not hang: dispatch
        // count still increments via the recording decorator even when HandledCount stays at 0.
        var pump = ConformanceDeferredPump.CreateReactor(channel, 3, TimeSpan.FromMilliseconds(5000));
        var pumping = Task.Factory.StartNew(() => pump.Run(), TaskCreationOptions.LongRunning);

        var key = ConformanceDeferredPump.KeyOf(message);
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(90)
               && ConformanceDeferredPump.GetDispatchCount(key) < 3)
        {
            await Task.Delay(100);
        }

        channel.Enqueue(MessageFactory.CreateQuitMessage(routingKey));
        await pumping;

        // Wait long enough that a native redrive at count 5 would have landed
        // (5 × lockTimeout = 5 × 5 s = 25 s), so the "native target empty" assertion is meaningful.
        await Task.Delay(TimeSpan.FromSeconds(30));

        // Assert — R-8 / AC-8: Brighter DLQ holds the message with rejectionReason
        var dlqMessage = dlqChannel.Receive(TimeSpan.FromMilliseconds(5000));

        Assert.NotEqual(MessageType.MT_NONE, dlqMessage.Header.MessageType);

        // R-5: rejection reason must be DeliveryError (Brighter fired first, not native redrive)
        Assert.True(dlqMessage.Header.Bag.ContainsKey("rejectionReason"),
            "Brighter DLQ must carry rejectionReason");
        Assert.Equal(RejectionReason.DeliveryError.ToString(),
            dlqMessage.Header.Bag["rejectionReason"].ToString());

        // R-8 / AC-8: native redrive target must be empty — Brighter deleted the original at count 3
        // before SQS could reach its own threshold of 5.
        var nativeMessage = nativeTargetChannel.Receive(TimeSpan.FromMilliseconds(5000));

        Assert.Equal(MessageType.MT_NONE, nativeMessage.Header.MessageType);
    }

    public void Dispose()
    {
        _channelFactory.DeleteTopicAsync().Wait();
        _channelFactory.DeleteQueueAsync().Wait();
        _dlqChannelFactory?.DeleteTopicAsync().Wait();
        _dlqChannelFactory?.DeleteQueueAsync().Wait();
        _nativeTargetChannelFactory?.DeleteTopicAsync().Wait();
        _nativeTargetChannelFactory?.DeleteQueueAsync().Wait();
    }

    public async ValueTask DisposeAsync()
    {
        await _channelFactory.DeleteTopicAsync();
        await _channelFactory.DeleteQueueAsync();
        if (_dlqChannelFactory is not null)
        {
            await _dlqChannelFactory.DeleteTopicAsync();
            await _dlqChannelFactory.DeleteQueueAsync();
        }

        if (_nativeTargetChannelFactory is not null)
        {
            await _nativeTargetChannelFactory.DeleteTopicAsync();
            await _nativeTargetChannelFactory.DeleteQueueAsync();
        }
    }
}
