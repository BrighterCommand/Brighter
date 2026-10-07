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
/// V4 lockstep twin of the v3 <c>SqsBudgetMinusOneNeverRejectsReactorTests</c> (NFR-6, R-12).
/// Characterises R-6 / AC-5: with <c>requeueCount: -1</c>, the budget is disabled,
/// <c>DiscardRequeuedMessagesEnabled()</c> returns <c>false</c>, the message is requeued
/// indefinitely, and no <c>DeliveryError</c> rejection is ever issued.
/// After a 60-second run the dispatch count exceeds 3 and the Brighter DLQ is empty.
/// </summary>
/// <remarks>
/// A single <c>rawMessageDelivery: true</c> case is used because each run costs ≥ 60 s; a
/// <c>[Theory]</c> over both message creators would double the runtime without adding coverage
/// the AC-5 characterisation needs.
/// </remarks>
[Trait("Category", "AWS")]
[Collection("SqsStandard")]
public class SqsBudgetMinusOneNeverRejectsReactorTests : IDisposable, IAsyncDisposable
{
    private readonly AWSMessagingGatewayConnection _awsConnection;
    private readonly ChannelFactory _channelFactory;
    private ChannelFactory? _dlqChannelFactory;

    public SqsBudgetMinusOneNeverRejectsReactorTests()
    {
        ConformanceDeferredPump.ResetDispatchCount();
        _awsConnection = GatewayFactory.CreateFactory();
        _channelFactory = new ChannelFactory(_awsConnection);
    }

    /// <summary>
    /// With <c>requeueCount: -1</c>, no <c>RedrivePolicy</c>, and a Brighter DLQ routing key,
    /// the pump runs for 60 s.  The dispatch count exceeds 3, no <c>DeliveryError</c> rejection
    /// is issued, and the Brighter DLQ is empty (R-6, AC-5).
    /// </summary>
    [Fact]
    public async Task When_sqs_budget_is_minus_one_should_never_reject()
    {
        // Arrange
        var topicName = $"M1V4-Reactor-{Guid.NewGuid()}".Truncate(45);
        var queueName = $"M1V4-Reactor-{Guid.NewGuid()}".Truncate(45);
        var dlqQueueName = $"M1V4-DLQ-{Guid.NewGuid()}".Truncate(45);

        var routingKey = new RoutingKey(topicName);
        var channelName = new ChannelName(queueName);
        var dlqRoutingKey = new RoutingKey(dlqQueueName);
        var dlqChannelName = new ChannelName(dlqQueueName);

        // Subscription: budget = -1 (disabled), no native RedrivePolicy, with a Brighter DLQ
        // routing key.  DiscardRequeuedMessagesEnabled() returns false, so HandledCountReached is
        // never called and the message is requeued indefinitely.
        var subscription = new SqsSubscription<ConformanceDeferredCommand>(
            subscriptionName: new SubscriptionName(queueName),
            channelName: channelName,
            channelType: ChannelType.PubSub,
            routingKey: routingKey,
            requeueCount: -1,
            requeueDelay: TimeSpan.Zero,
            messagePumpType: MessagePumpType.Reactor,
            queueAttributes: new SqsAttributes(
                rawMessageDelivery: true,
                lockTimeout: TimeSpan.FromSeconds(5)),
            makeChannels: OnMissingChannel.Create,
            topicAttributes: new SnsAttributes(tags: [new Tag { Key = "Environment", Value = "Test" }]),
            deadLetterRoutingKey: dlqRoutingKey);

        _channelFactory.CreateSyncChannel(subscription);

        // Create the Brighter DLQ so that, under the RED mutation (DiscardRequeuedMessagesEnabled
        // returns true), the pump has somewhere to dead-letter and the test fails on the correct
        // assertion ("dispatch count > 3") rather than an unrelated exception.
        var dlqSubscription = new SqsSubscription<ConformanceDeferredCommand>(
            subscriptionName: new SubscriptionName($"M1V4-DLQRdr-{Guid.NewGuid()}".Truncate(45)),
            channelName: dlqChannelName,
            channelType: ChannelType.PointToPoint,
            routingKey: dlqRoutingKey,
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Create);

        _dlqChannelFactory = new ChannelFactory(_awsConnection);
        var dlqChannel = _dlqChannelFactory.CreateSyncChannel(dlqSubscription);

        // Wrap the underlying consumer in a recording decorator so dispatch counts are observable.
        var consumer = new SqsMessageConsumerFactory(_awsConnection).Create(subscription);
        var channel = ConformanceDeferredPump.CreateRecordingChannel(channelName, routingKey, consumer);

        var producer = new SnsMessageProducer(_awsConnection, new SnsPublication
        {
            Topic = routingKey,
            RequestType = typeof(ConformanceDeferredCommand),
            MakeChannels = OnMissingChannel.Validate
        });

        var cmd = new ConformanceDeferredCommand { Value = "budget minus one never rejects test" };
        var message = new Message(
            new MessageHeader(cmd.Id, routingKey, MessageType.MT_COMMAND),
            new MessageBody(JsonSerializer.Serialize(cmd, JsonSerialisationOptions.Options)));

        producer.Send(message);

        // Act — run the pump with budget -1; pump for 60 s, then quit and await it.
        // With lockTimeout = 5 s, the message becomes visible ~12 times in 60 s, so dispatch
        // count will far exceed 3.
        // Under the RED mutation (DiscardRequeuedMessagesEnabled returns true),
        // HandledCountReached(-1) is true on the first deferral (HandledCount 0 >= -1), so
        // dispatch count stays at 1 and the DLQ receives the message — the test then fails on
        // the "dispatch count > 3" assertion.
        var pump = ConformanceDeferredPump.CreateReactor(channel, -1, TimeSpan.FromMilliseconds(5000));
        var pumping = Task.Factory.StartNew(() => pump.Run(), TaskCreationOptions.LongRunning);

        await Task.Delay(TimeSpan.FromSeconds(60));

        channel.Enqueue(MessageFactory.CreateQuitMessage(routingKey));
        await pumping;

        var key = ConformanceDeferredPump.KeyOf(message);
        var dispatchCount = ConformanceDeferredPump.GetDispatchCount(key);

        // Assert — R-6 / AC-5: handler invoked more than 3 times with budget -1
        Assert.True(dispatchCount > 3,
            $"Expected dispatch count > 3 after 60 s with requeueCount: -1, but was {dispatchCount}");

        // No DeliveryError rejection was issued — the Brighter DLQ must be empty
        var dlqMessage = dlqChannel.Receive(TimeSpan.FromMilliseconds(5000));

        Assert.Equal(MessageType.MT_NONE, dlqMessage.Header.MessageType);
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
