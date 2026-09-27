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

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Sqs.Standard.Proactor;

/// <summary>
/// Async (Proactor) twin of <c>SqsNativeRedriveLimitBelowBudgetReactorTests</c>.
/// Characterises R-9 / AC-9: when the native SQS redrive limit (<c>RedrivePolicy.maxReceiveCount: 3</c>)
/// is below the Brighter budget (<c>requeueCount: 10</c>), SQS performs the native redrive itself at
/// count 3.  Brighter neither suppresses nor stamps the native copy, so the message reaches the native
/// redrive target with <b>none</b> of the five <see cref="RejectionMetadataKeyNames"/> keys.
/// A Brighter DLQ is wired on the subscription so that, under the mutation (DeliveryCount.Resolve
/// over-counts), Brighter has somewhere to dead-letter; without a DLQ the mutation would fail with an
/// unrelated exception rather than the expected "native target holds the message" failure.
/// Covers both message creators via <paramref name="rawMessageDelivery"/>:
/// <c>true</c> → <c>SqsMessageCreator</c>; <c>false</c> → <c>SqsInlineMessageCreator</c>.
/// </summary>
[Trait("Category", "AWS")]
[Collection("SqsStandard")]
public class SqsNativeRedriveLimitBelowBudgetProactorTests : IDisposable, IAsyncDisposable
{
    private readonly AWSMessagingGatewayConnection _awsConnection;
    private readonly ChannelFactory _channelFactory;
    private ChannelFactory? _dlqChannelFactory;
    private ChannelFactory? _nativeTargetChannelFactory;

    public SqsNativeRedriveLimitBelowBudgetProactorTests()
    {
        ConformanceDeferredPump.ResetDispatchCount();
        _awsConnection = GatewayFactory.CreateFactory();
        _channelFactory = new ChannelFactory(_awsConnection);
    }

    /// <summary>
    /// With <c>requeueCount: 10</c> and <c>RedrivePolicy(maxReceiveCount: 3)</c>, SQS exhausts its
    /// native limit first.  The native redrive target holds the message and none of the five
    /// <see cref="RejectionMetadataKeyNames"/> keys are present in the copy (R-9, AC-9).
    /// No <c>HandledCount</c> is asserted (AC-41's "no count asserted" clause).
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task When_sqs_native_redrive_limit_is_below_budget_should_redrive_without_rejection_metadata_async(
        bool rawMessageDelivery)
    {
        // Arrange
        var topicName = $"NBL-Proactor-{Guid.NewGuid()}".Truncate(45);
        var queueName = $"NBL-Proactor-{Guid.NewGuid()}".Truncate(45);
        var dlqQueueName = $"NBL-DLQ-{Guid.NewGuid()}".Truncate(45);          // Brighter DLQ (mutation safety net)
        var nativeTargetQueueName = $"NBL-NTG-{Guid.NewGuid()}".Truncate(45); // SQS native redrive target

        var routingKey = new RoutingKey(topicName);
        var channelName = new ChannelName(queueName);
        var dlqRoutingKey = new RoutingKey(dlqQueueName);
        var dlqChannelName = new ChannelName(dlqQueueName);
        var nativeTargetChannelName = new ChannelName(nativeTargetQueueName);
        var nativeTargetRoutingKey = new RoutingKey(nativeTargetQueueName);

        // Subscription: Brighter budget = 10, native SQS redrive limit = 3.
        // SQS fires first because 3 < 10. A Brighter DLQ is wired so the mutation
        // (over-counted DeliveryCount.Resolve) has somewhere to dead-letter, causing the correct
        // test failure ("native target holds the message" → MT_NONE) rather than an exception.
        var subscription = new SqsSubscription<ConformanceDeferredCommand>(
            subscriptionName: new SubscriptionName(queueName),
            channelName: channelName,
            channelType: ChannelType.PubSub,
            routingKey: routingKey,
            requeueCount: 10,
            requeueDelay: TimeSpan.Zero,
            messagePumpType: MessagePumpType.Proactor,
            queueAttributes: new SqsAttributes(
                rawMessageDelivery: rawMessageDelivery,
                lockTimeout: TimeSpan.FromSeconds(5),
                redrivePolicy: new RedrivePolicy(nativeTargetChannelName, 3)),
            makeChannels: OnMissingChannel.Create,
            topicAttributes: new SnsAttributes(tags: [new Tag { Key = "Environment", Value = "Test" }]),
            deadLetterRoutingKey: dlqRoutingKey);

        // Create SNS topic, SQS source queue, and native target queue (via redrivePolicy)
        _channelFactory.CreateAsyncChannel(subscription);

        // Create the Brighter DLQ so rejected messages (only under mutation) have somewhere to land
        var dlqSubscription = new SqsSubscription<ConformanceDeferredCommand>(
            subscriptionName: new SubscriptionName($"NBL-DLQ-Rdr-{Guid.NewGuid()}".Truncate(45)),
            channelName: dlqChannelName,
            channelType: ChannelType.PointToPoint,
            routingKey: dlqRoutingKey,
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: OnMissingChannel.Create);

        _dlqChannelFactory = new ChannelFactory(_awsConnection);
        _dlqChannelFactory.CreateAsyncChannel(dlqSubscription);

        // Create a reader channel for the native target (already provisioned by redrivePolicy above)
        var nativeTargetSubscription = new SqsSubscription<ConformanceDeferredCommand>(
            subscriptionName: new SubscriptionName($"NBL-NTG-Rdr-{Guid.NewGuid()}".Truncate(45)),
            channelName: nativeTargetChannelName,
            channelType: ChannelType.PointToPoint,
            routingKey: nativeTargetRoutingKey,
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: OnMissingChannel.Create);

        _nativeTargetChannelFactory = new ChannelFactory(_awsConnection);
        var nativeTargetChannel = _nativeTargetChannelFactory.CreateAsyncChannel(nativeTargetSubscription);

        // Wrap the underlying async consumer in a recording decorator so dispatch counts are observable
        var asyncConsumer = new SqsMessageConsumerFactory(_awsConnection).CreateAsync(subscription);
        var channel = ConformanceDeferredPump.CreateRecordingChannelAsync(channelName, routingKey, asyncConsumer);

        var producer = new SnsMessageProducer(_awsConnection, new SnsPublication
        {
            Topic = routingKey,
            RequestType = typeof(ConformanceDeferredCommand),
            MakeChannels = OnMissingChannel.Validate
        });

        var cmd = new ConformanceDeferredCommand { Value = "native redrive limit below budget test async" };
        var message = new Message(
            new MessageHeader(cmd.Id, routingKey, MessageType.MT_COMMAND),
            new MessageBody(JsonSerializer.Serialize(cmd, JsonSerialisationOptions.Options)));

        await producer.SendAsync(message);

        // Act — run the pump with budget 10; wait until 3 dispatches (ReceiveCount=3=maxReceiveCount),
        // then allow one more pump cycle so ReceiveCount exceeds maxReceiveCount (4th receive triggers
        // native SQS redrive: SQS either returns the message with ReceiveCount=4 then moves it to the
        // native target, or moves it silently and returns empty — either way the native target fills).
        // Under the mutation DeliveryCount.Resolve returns Normalise(brokerCount)*5, the third receive
        // presents count 10, Brighter dead-letters to the Brighter DLQ, and the native target stays
        // empty; the 90 s bound prevents a hang while waiting for a 4th dispatch that never comes.
        var pump = ConformanceDeferredPump.CreateProactor(channel, 10, TimeSpan.FromMilliseconds(5000));
        var pumping = Task.Factory.StartNew(() => pump.Run(), TaskCreationOptions.LongRunning);

        var key = ConformanceDeferredPump.KeyOf(message);
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(90)
               && ConformanceDeferredPump.GetDispatchCount(key) < 3)
        {
            await Task.Delay(100);
        }

        // Allow time for the pump to complete its current cycle and attempt one more receive.
        // The 4th receive (ReceiveCount=4 > maxReceiveCount=3) triggers the native SQS DLQ move.
        await Task.Delay(TimeSpan.FromSeconds(5));

        channel.Enqueue(MessageFactory.CreateQuitMessage(routingKey));
        await pumping;

        // Wait for the native redrive copy to be available in the native target.
        // The move is near-instantaneous once triggered; 10 s gives comfortable headroom.
        await Task.Delay(TimeSpan.FromSeconds(10));

        // Assert — R-9 / AC-9: the native target holds the message (SQS performed the redrive)
        var nativeMessage = await nativeTargetChannel.ReceiveAsync(TimeSpan.FromMilliseconds(5000));

        Assert.NotEqual(MessageType.MT_NONE, nativeMessage.Header.MessageType);

        // R-9: the native copy carries NONE of the five Brighter rejection metadata keys
        Assert.False(nativeMessage.Header.Bag.ContainsKey(RejectionMetadataKeyNames.RejectionReason),
            $"Native redrive copy must not carry '{RejectionMetadataKeyNames.RejectionReason}'");
        Assert.False(nativeMessage.Header.Bag.ContainsKey(RejectionMetadataKeyNames.RejectionMessage),
            $"Native redrive copy must not carry '{RejectionMetadataKeyNames.RejectionMessage}'");
        Assert.False(nativeMessage.Header.Bag.ContainsKey(RejectionMetadataKeyNames.RejectionTimestamp),
            $"Native redrive copy must not carry '{RejectionMetadataKeyNames.RejectionTimestamp}'");
        Assert.False(nativeMessage.Header.Bag.ContainsKey(RejectionMetadataKeyNames.OriginalTopic),
            $"Native redrive copy must not carry '{RejectionMetadataKeyNames.OriginalTopic}'");
        Assert.False(nativeMessage.Header.Bag.ContainsKey(RejectionMetadataKeyNames.OriginalMessageType),
            $"Native redrive copy must not carry '{RejectionMetadataKeyNames.OriginalMessageType}'");
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
