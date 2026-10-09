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

using System.Collections.Generic;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway;

/// <summary>
/// Verifies that every DLQ-backed AWS conformance provider (SqsStandard, SqsFifo, SnsStandard,
/// SnsFifo) holds its budget strictly below the native redrive limit (R-27(a), AC-36, A-5):
/// <c>requeueCount == 3</c> (the pump budget) and <c>NativeRedriveLimit == 5</c> (the SQS
/// maxReceiveCount), so the pump always exhausts the budget before SQS moves the message.
/// These assertions are made on the constructed <see cref="SqsSubscription"/> without touching
/// the broker.
/// </summary>
[Trait("Category", "AWS")]
public class ConformanceProviderNativeRedriveLimitTests
{
    public static IEnumerable<object[]> DlqBackedSubscriptions()
    {
        // Arrange — build each DLQ-backed subscription through its conformance provider.
        // CreateSubscription is a pure construction call; no broker contact is made.

        yield return
        [
            new SqsStandardMessageGatewayProvider().CreateSubscription(
                routingKey: new RoutingKey("test-sqs-std-queue"),
                channelName: new ChannelName("test-sqs-std-channel"),
                makeChannel: OnMissingChannel.Assume,
                deadLetterRoutingKey: new RoutingKey("test-sqs-std-dlq")
            )
        ];

        yield return
        [
            new SqsFifoMessageGatewayProvider().CreateSubscription(
                routingKey: new RoutingKey("test-sqs-fifo-queue.fifo"),
                channelName: new ChannelName("test-sqs-fifo-channel.fifo"),
                makeChannel: OnMissingChannel.Assume,
                deadLetterRoutingKey: new RoutingKey("test-sqs-fifo-dlq")
            )
        ];

        yield return
        [
            new SnsStandardMessageGatewayProvider().CreateSubscription(
                routingKey: new RoutingKey("test-sns-std-topic"),
                channelName: new ChannelName("test-sns-std-channel"),
                makeChannel: OnMissingChannel.Assume,
                deadLetterRoutingKey: new RoutingKey("test-sns-std-dlq")
            )
        ];

        yield return
        [
            new SnsFifoMessageGatewayProvider().CreateSubscription(
                routingKey: new RoutingKey("test-sns-fifo-topic.fifo"),
                channelName: new ChannelName("test-sns-fifo-channel.fifo"),
                makeChannel: OnMissingChannel.Assume,
                deadLetterRoutingKey: new RoutingKey("test-sns-fifo-dlq")
            )
        ];
    }

    [Theory]
    [MemberData(nameof(DlqBackedSubscriptions))]
    public void When_reading_conformance_providers_should_hold_budget_below_native_limit(
        SqsSubscription subscription)
    {
        // Assert — the pump budget (requeueCount) is strictly below the native redrive limit
        // so the pump always exhausts the budget before SQS moves the message to the DLQ.
        Assert.Equal(3, subscription.RequeueCount);

        var counting = Assert.IsAssignableFrom<IAmADeliveryCountingSubscription>(subscription);
        Assert.Equal(5, counting.NativeRedriveLimit);
    }
}
