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

namespace Paramore.Brighter.AWS.V4.Tests.MessagingGateway.Sqs.Standard.Proactor;

/// <summary>
/// Async (Proactor) twin of the V4 <c>SqsRedeliveryDeliveryCountTests</c> (NFR-6, NFR-8, R-12).
/// Verifies that SQS presents a strictly increasing delivery count across redeliveries, starting
/// at 0 (R-1, R-2, R-3, AC-1, AC-2).  Covers both message creators via the
/// <paramref name="rawMessageDelivery"/> theory parameter:
/// <c>true</c> → <c>SqsMessageCreator</c>; <c>false</c> → <c>SqsInlineMessageCreator</c>.
/// </summary>
[Trait("Category", "AWS")]
[Collection("SqsStandard")]
public class SqsRedeliveryDeliveryCountTestsAsync : IDisposable, IAsyncDisposable
{
    private readonly AWSMessagingGatewayConnection _awsConnection;
    private readonly ChannelFactory _channelFactory;

    public SqsRedeliveryDeliveryCountTestsAsync()
    {
        ConformanceDeferredPump.ResetDispatchCount();
        _awsConnection = GatewayFactory.CreateFactory();
        _channelFactory = new ChannelFactory(_awsConnection);
    }

    /// <summary>
    /// A pump running over a deferring handler with <c>requeueCount: -1</c> presents a strictly
    /// increasing <c>HandledCount</c> on each redelivery, and the first delivery presents <c>0</c>.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task When_an_sqs_message_is_redelivered_should_present_increasing_delivery_count_async(bool rawMessageDelivery)
    {
        // Arrange
        var topicName = $"Dc-Proactor-{Guid.NewGuid()}".Truncate(45);
        var queueName = $"Dc-Proactor-{Guid.NewGuid()}".Truncate(45);
        var routingKey = new RoutingKey(topicName);
        var channelName = new ChannelName(queueName);

        var subscription = new SqsSubscription<ConformanceDeferredCommand>(
            subscriptionName: new SubscriptionName(queueName),
            channelName: channelName,
            channelType: ChannelType.PubSub,
            routingKey: routingKey,
            requeueCount: -1,
            requeueDelay: TimeSpan.Zero,
            messagePumpType: MessagePumpType.Proactor,
            queueAttributes: new SqsAttributes(
                rawMessageDelivery: rawMessageDelivery,
                lockTimeout: TimeSpan.FromSeconds(30)),
            makeChannels: OnMissingChannel.Create,
            topicAttributes: new SnsAttributes(tags: [new Tag { Key = "Environment", Value = "Test" }]));

        // Create the SNS topic, SQS queue and SNS subscription; discard the channel returned by the
        // factory — we wrap the underlying consumer ourselves so the recording decorator can observe it.
        _channelFactory.CreateAsyncChannel(subscription);

        var asyncConsumer = new SqsMessageConsumerFactory(_awsConnection).CreateAsync(subscription);
        var channel = ConformanceDeferredPump.CreateRecordingChannelAsync(channelName, routingKey, asyncConsumer);

        var producer = new SnsMessageProducer(_awsConnection, new SnsPublication
        {
            Topic = routingKey,
            RequestType = typeof(ConformanceDeferredCommand),
            MakeChannels = OnMissingChannel.Validate
        });

        var cmd = new ConformanceDeferredCommand { Value = "delivery count test async" };
        var message = new Message(
            new MessageHeader(cmd.Id, routingKey, MessageType.MT_COMMAND),
            new MessageBody(JsonSerializer.Serialize(cmd, JsonSerialisationOptions.Options)));

        await producer.SendAsync(message);

        // Act — run the production pump with no budget; quit when the handler has been invoked 3 times
        var pump = ConformanceDeferredPump.CreateProactor(channel, -1, TimeSpan.FromMilliseconds(5000));
        var pumping = Task.Factory.StartNew(() => pump.Run(), TaskCreationOptions.LongRunning);

        var key = ConformanceDeferredPump.KeyOf(message);
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(60)
               && ConformanceDeferredPump.GetDispatchCount(key) < 3)
        {
            await Task.Delay(100);
        }

        channel.Enqueue(MessageFactory.CreateQuitMessage(routingKey));
        await pumping;

        // Assert — read HandledCount sequence after pump quit-and-await (R-27(c) observation window)
        var counts = ConformanceDeferredPump.GetHandledCountLog(key);
        Assert.True(counts.Count >= 3,
            $"expected at least 3 deliveries but recorded {counts.Count}");

        // R-2 / AC-2: the first delivery presents 0
        Assert.Equal(0, counts[0]);

        // R-1 / AC-1: every subsequent delivery presents a strictly greater count
        for (var i = 1; i < counts.Count; i++)
        {
            Assert.True(counts[i] > counts[i - 1],
                $"R-1: delivery {i + 1} count {counts[i]} must be strictly greater than "
                + $"delivery {i} count {counts[i - 1]}");
        }
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
