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
using Paramore.Brighter.AWS.V4.Tests.Helpers;
using Paramore.Brighter.AWS.V4.Tests.MessagingGateway.SqsStandard;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.AWSSQS.V4;
using Xunit;

namespace Paramore.Brighter.AWS.V4.Tests.MessagingGateway.Sqs.Standard.Reactor;

/// <summary>
/// V4 lockstep twin of the v3
/// <c>SqsBudgetOneZeroOrBelowMinusOneRejectsOnFirstDeferralReactorTests</c> (NFR-6, R-12).
/// Characterises R-7 / AC-6 / AC-35: with a budget of <c>1</c>, <c>0</c>, or any value below
/// <c>-1</c> (e.g. <c>-3</c>), <c>DiscardRequeuedMessagesEnabled()</c> returns <c>true</c> and
/// <c>HandledCountReached</c> is satisfied on the first deferral, so the message is rejected
/// with a <c>DeliveryError</c> reason without ever being requeued.
/// </summary>
[Trait("Category", "AWS")]
public class SqsBudgetOneZeroOrBelowMinusOneRejectsOnFirstDeferralReactorV4Tests : IDisposable, IAsyncDisposable
{
    private readonly AWSMessagingGatewayConnection _awsConnection;
    private readonly ChannelFactory _channelFactory;
    private ChannelFactory? _dlqChannelFactory;

    public SqsBudgetOneZeroOrBelowMinusOneRejectsOnFirstDeferralReactorV4Tests()
    {
        ConformanceDeferredPump.ResetDispatchCount();
        _awsConnection = GatewayFactory.CreateFactory();
        _channelFactory = new ChannelFactory(_awsConnection);
    }

    /// <summary>
    /// With a budget of <paramref name="requeueCount"/>, the handler is invoked exactly once,
    /// the recording consumer's Requeue count is 0, and a <c>DeliveryError</c> rejection is
    /// issued placing the message on the Brighter DLQ (R-7, AC-6, AC-35).
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task When_sqs_budget_is_one_zero_or_below_minus_one_should_reject_on_first_deferral(
        int requeueCount)
    {
        // Arrange
        var tag = requeueCount < 0 ? $"N{Math.Abs(requeueCount)}" : requeueCount.ToString();
        var topicName = $"R7-Reactor-{tag}-{Guid.NewGuid()}".Truncate(45);
        var queueName = $"R7-Reactor-{tag}-{Guid.NewGuid()}".Truncate(45);
        var dlqQueueName = $"R7-DLQ-{tag}-{Guid.NewGuid()}".Truncate(45);

        var routingKey = new RoutingKey(topicName);
        var channelName = new ChannelName(queueName);
        var dlqRoutingKey = new RoutingKey(dlqQueueName);
        var dlqChannelName = new ChannelName(dlqQueueName);

        // Subscription: budget = requeueCount, no native RedrivePolicy, with a Brighter DLQ
        // routing key.  DiscardRequeuedMessagesEnabled() returns true for all three values,
        // and HandledCountReached fires on the first deferral.
        var subscription = new SqsSubscription<ConformanceDeferredCommand>(
            subscriptionName: new SubscriptionName(queueName),
            channelName: channelName,
            channelType: ChannelType.PubSub,
            routingKey: routingKey,
            requeueCount: requeueCount,
            requeueDelay: TimeSpan.Zero,
            messagePumpType: MessagePumpType.Reactor,
            queueAttributes: new SqsAttributes(
                rawMessageDelivery: true,
                lockTimeout: TimeSpan.FromSeconds(30)),
            makeChannels: OnMissingChannel.Create,
            topicAttributes: new SnsAttributes(tags: [new Tag { Key = "Environment", Value = "Test" }]),
            deadLetterRoutingKey: dlqRoutingKey);

        _channelFactory.CreateSyncChannel(subscription);

        // Create the DLQ queue so the rejected message has somewhere to land
        var dlqSubscription = new SqsSubscription<ConformanceDeferredCommand>(
            subscriptionName: new SubscriptionName($"R7-DLQ-Rdr-{tag}-{Guid.NewGuid()}".Truncate(45)),
            channelName: dlqChannelName,
            channelType: ChannelType.PointToPoint,
            routingKey: dlqRoutingKey,
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Create);

        _dlqChannelFactory = new ChannelFactory(_awsConnection);
        var dlqChannel = _dlqChannelFactory.CreateSyncChannel(dlqSubscription);

        // Wrap the underlying consumer in a recording decorator so dispatch and requeue counts
        // are observable.
        var consumer = new SqsMessageConsumerFactory(_awsConnection).Create(subscription);
        var channel = ConformanceDeferredPump.CreateRecordingChannel(channelName, routingKey, consumer);

        var producer = new SnsMessageProducer(_awsConnection, new SnsPublication
        {
            Topic = routingKey,
            RequestType = typeof(ConformanceDeferredCommand),
            MakeChannels = OnMissingChannel.Validate
        });

        var cmd = new ConformanceDeferredCommand { Value = $"budget {requeueCount} rejects on first deferral" };
        var message = new Message(
            new MessageHeader(cmd.Id, routingKey, MessageType.MT_COMMAND),
            new MessageBody(JsonSerializer.Serialize(cmd, JsonSerialisationOptions.Options)));

        producer.Send(message);

        // Act — run the pump; correct behaviour rejects immediately on the first deferral.
        // Under mutation (a) (DiscardRequeuedMessagesEnabled returns RequeueCount > 0 instead of
        // RequeueCount != -1), the 0 and -3 rows requeue forever — we bound the run by polling
        // until the DLQ copy arrives or a 30-second window elapses, then sending Quit.
        var pump = ConformanceDeferredPump.CreateReactor(channel, requeueCount, TimeSpan.FromMilliseconds(5000));
        var pumping = Task.Factory.StartNew(() => pump.Run(), TaskCreationOptions.LongRunning);

        var key = ConformanceDeferredPump.KeyOf(message);

        // Poll until DLQ copy arrives (correct behaviour) or 30 s passes (mutation scenario).
        var stopwatch = Stopwatch.StartNew();
        Message dlqMessage;
        do
        {
            dlqMessage = dlqChannel.Receive(TimeSpan.FromMilliseconds(2000));
            if (dlqMessage.Header.MessageType != MessageType.MT_NONE)
                break;
        } while (stopwatch.Elapsed < TimeSpan.FromSeconds(30));

        channel.Enqueue(MessageFactory.CreateQuitMessage(routingKey));
        await pumping;

        // Assert — R-7 / AC-6 / AC-35

        // Under mutation (b) HandledCountReached uses > instead of >=, so the 1 row requeues
        // once before rejecting; requeue count is 1 instead of 0.  Checked first so this
        // assertion is the one that surfaces under mutation (b).
        var requeueCountActual = ConformanceDeferredPump.GetRequeueCount(key);
        Assert.Equal(0, requeueCountActual);

        // Under mutation (a) the 0 and -3 rows requeue forever, so dispatch count > 1 and the
        // assertion below fails.
        var dispatchCount = ConformanceDeferredPump.GetDispatchCount(key);
        Assert.Equal(1, dispatchCount);

        // A DeliveryError rejection was issued — the DLQ copy must carry rejectionReason.
        Assert.NotEqual(MessageType.MT_NONE, dlqMessage.Header.MessageType);

        Assert.True(dlqMessage.Header.Bag.ContainsKey("rejectionReason"),
            "DLQ bag must carry rejectionReason");
        Assert.Equal(RejectionReason.DeliveryError.ToString(),
            dlqMessage.Header.Bag["rejectionReason"].ToString());
    }

    public void Dispose()
    {
        _channelFactory.DeleteTopicAsync().Wait();
        _channelFactory.DeleteQueueAsync().Wait();
        _dlqChannelFactory?.DeleteTopicAsync().Wait();
        _dlqChannelFactory?.DeleteQueueAsync().Wait();
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
    }
}
