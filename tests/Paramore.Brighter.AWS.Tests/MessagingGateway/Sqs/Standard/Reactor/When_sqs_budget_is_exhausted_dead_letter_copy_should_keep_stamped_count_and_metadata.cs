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
/// Characterises that the SQS dead-letter copy of a budget-exhausted message, read through a
/// real channel, carries the stamped <see cref="MessageHeader.HandledCount"/> and the full
/// rejection metadata (R-5, R-28, AC-4, AC-41).
/// Covers both message creators via <paramref name="rawMessageDelivery"/>:
/// <c>true</c> → <c>SqsMessageCreator</c>; <c>false</c> → <c>SqsInlineMessageCreator</c>.
/// </summary>
[Trait("Category", "AWS")]
[Collection("SqsStandard")]
public class SqsBudgetExhaustedDlqTests : IDisposable, IAsyncDisposable
{
    private readonly AWSMessagingGatewayConnection _awsConnection;
    private readonly ChannelFactory _channelFactory;
    private ChannelFactory? _dlqChannelFactory;

    public SqsBudgetExhaustedDlqTests()
    {
        ConformanceDeferredPump.ResetDispatchCount();
        _awsConnection = GatewayFactory.CreateFactory();
        _channelFactory = new ChannelFactory(_awsConnection);
    }

    /// <summary>
    /// A pump running over a deferring handler with <c>requeueCount: 3</c> and a Brighter DLQ
    /// sends the message to the DLQ after the budget is exhausted.  The DLQ copy carries
    /// <c>HandledCount >= 3</c> (stamped — not the DLQ's own normalised counter, which is 0)
    /// and the full rejection metadata bag.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task When_sqs_budget_is_exhausted_dead_letter_copy_should_keep_stamped_count_and_metadata(
        bool rawMessageDelivery)
    {
        // Arrange
        var topicName = $"BX-Reactor-{Guid.NewGuid()}".Truncate(45);
        var queueName = $"BX-Reactor-{Guid.NewGuid()}".Truncate(45);
        var dlqQueueName = $"BX-DLQ-{Guid.NewGuid()}".Truncate(45);
        var routingKey = new RoutingKey(topicName);
        var channelName = new ChannelName(queueName);
        var dlqRoutingKey = new RoutingKey(dlqQueueName);
        var dlqChannelName = new ChannelName(dlqQueueName);

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
                lockTimeout: TimeSpan.FromSeconds(30)),
            makeChannels: OnMissingChannel.Create,
            topicAttributes: new SnsAttributes(tags: [new Tag { Key = "Environment", Value = "Test" }]),
            deadLetterRoutingKey: dlqRoutingKey);

        // Create the SNS topic, SQS queue, and SNS subscription
        _channelFactory.CreateSyncChannel(subscription);

        // Create the DLQ queue so rejected messages have somewhere to land
        var dlqSubscription = new SqsSubscription<ConformanceDeferredCommand>(
            subscriptionName: new SubscriptionName($"BX-DLQ-Rdr-{Guid.NewGuid()}".Truncate(45)),
            channelName: dlqChannelName,
            channelType: ChannelType.PointToPoint,
            routingKey: dlqRoutingKey,
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Create);

        _dlqChannelFactory = new ChannelFactory(_awsConnection);
        var dlqChannel = _dlqChannelFactory.CreateSyncChannel(dlqSubscription);

        // Wrap the underlying consumer in a recording decorator so dispatch counts are observable
        var consumer = new SqsMessageConsumerFactory(_awsConnection).Create(subscription);
        var channel = ConformanceDeferredPump.CreateRecordingChannel(channelName, routingKey, consumer);

        var producer = new SnsMessageProducer(_awsConnection, new SnsPublication
        {
            Topic = routingKey,
            RequestType = typeof(ConformanceDeferredCommand),
            MakeChannels = OnMissingChannel.Validate
        });

        var cmd = new ConformanceDeferredCommand { Value = "budget exhaustion test" };
        var message = new Message(
            new MessageHeader(cmd.Id, routingKey, MessageType.MT_COMMAND),
            new MessageBody(JsonSerializer.Serialize(cmd, JsonSerialisationOptions.Options)));

        producer.Send(message);

        // Act — run the pump with budget 3; wait until 3 dispatches (budget exhausted, DLQ sent)
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

        // Assert — read the dead-letter copy via the ChannelFactory channel (AC-41 / R-28)
        var dlqMessage = dlqChannel.Receive(TimeSpan.FromMilliseconds(5000));

        Assert.NotEqual(MessageType.MT_NONE, dlqMessage.Header.MessageType);

        // AC-41: HandledCount must be >= 3 (stamped) and NOT 0 (the DLQ's own normalised counter)
        Assert.True(dlqMessage.Header.HandledCount >= 3,
            $"R-28: HandledCount must be >= 3 but was {dlqMessage.Header.HandledCount}");
        Assert.NotEqual(0, dlqMessage.Header.HandledCount);

        // AC-4 / R-5: rejection reason must be DeliveryError
        Assert.True(dlqMessage.Header.Bag.ContainsKey("rejectionReason"),
            "bag must carry rejectionReason");
        Assert.Equal(RejectionReason.DeliveryError.ToString(),
            dlqMessage.Header.Bag["rejectionReason"].ToString());

        // AC-4 / R-5: rejection message must be non-empty
        Assert.True(dlqMessage.Header.Bag.ContainsKey("rejectionMessage"),
            "bag must carry rejectionMessage");
        Assert.False(
            string.IsNullOrWhiteSpace(dlqMessage.Header.Bag["rejectionMessage"].ToString()),
            "rejectionMessage must be non-empty");

        // AC-4 / R-5: rejection timestamp must be ISO-8601 parseable
        Assert.True(dlqMessage.Header.Bag.ContainsKey("rejectionTimestamp"),
            "bag must carry rejectionTimestamp");
        Assert.True(
            DateTimeOffset.TryParse(dlqMessage.Header.Bag["rejectionTimestamp"].ToString(), out _),
            $"rejectionTimestamp must be ISO-8601 parseable but was "
            + $"'{dlqMessage.Header.Bag["rejectionTimestamp"]}'");

        // AC-4 / R-5: original topic must match the source topic
        Assert.True(dlqMessage.Header.Bag.ContainsKey("originalTopic"),
            "bag must carry originalTopic");
        Assert.Equal(routingKey.Value, dlqMessage.Header.Bag["originalTopic"].ToString());

        // AC-4 / R-5: original message type must be present
        Assert.True(dlqMessage.Header.Bag.ContainsKey("originalMessageType"),
            "bag must carry originalMessageType");
        Assert.Equal(MessageType.MT_COMMAND.ToString(),
            dlqMessage.Header.Bag["originalMessageType"].ToString());
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
