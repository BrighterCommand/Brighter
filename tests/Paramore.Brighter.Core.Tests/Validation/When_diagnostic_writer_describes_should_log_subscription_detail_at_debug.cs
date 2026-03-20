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

using System.Linq;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Core.Tests.Validation.TestDoubles;
using Paramore.Brighter.Validation;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Validation;

public class PipelineDiagnosticWriterSubscriptionDetailTests
{
    [Fact]
    public void When_diagnostic_writer_describes_should_log_subscription_detail_at_debug()
    {
        // Arrange — one subscription with known channel, routing key, and pump type
        var registry = new SubscriberRegistry();
        registry.Add(typeof(MyDescribableCommand), typeof(MyPublicSyncHandler));
        var pipelineBuilder = new PipelineBuilder<IRequest>(registry);
        PipelineBuilder<IRequest>.ClearPipelineCache();

        var subscriptions = new[]
        {
            new Subscription(
                subscriptionName: new SubscriptionName("order-sub"),
                channelName: new ChannelName("order-channel"),
                routingKey: new RoutingKey("order.created"),
                requestType: typeof(MyDescribableCommand),
                messagePumpType: MessagePumpType.Reactor)
        };

        var logger = new SpyLogger();
        var writer = new PipelineDiagnosticWriter(
            logger, pipelineBuilder, subscriptions: subscriptions);

        // Act
        writer.Describe();

        // Assert — Debug messages contain subscription section and detail
        var debugMessages = logger.DebugEntries.Select(e => e.Message).ToList();

        // Section header
        Assert.Contains(debugMessages, m => m.Contains("Subscriptions"));

        // Subscription name with pump type
        Assert.Contains(debugMessages, m =>
            m.Contains("order-sub") && m.Contains("Reactor"));

        // Channel and routing key
        Assert.Contains(debugMessages, m =>
            m.Contains("order-channel") && m.Contains("order.created"));
    }
}
