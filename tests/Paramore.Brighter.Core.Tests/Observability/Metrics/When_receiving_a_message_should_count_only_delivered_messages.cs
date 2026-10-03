#region Licence

/* The MIT License (MIT)
Copyright © 2026 gabisonia <irakli.gabisonia94@gmail.com>

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

#nullable enable

using System.Diagnostics;
using Paramore.Brighter.Core.Tests.Observability.TestDoubles;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.Metrics;

public class ConsumedMessageMetricsTests
{
    [Fact]
    public void When_receiving_a_message_should_count_only_delivered_messages()
    {
        //Arrange
        using var telemetry = new InMemoryMetricsFromTraces();
        var topic = new RoutingKey("consumed-message-metrics");
        Message?[] receiveResults =
        [
            new Message(),
            null,
            new Message(new MessageHeader(Id.Random(), topic, MessageType.MT_EVENT), new MessageBody("payload")),
            MessageFactory.CreateQuitMessage(topic)
        ];

        //Act
        foreach (var message in receiveResults)
        {
            var span = telemetry.Tracer.CreateReceiveSpan(topic, MessagingSystem.InternalBus);
            Assert.NotNull(span);
            if (message is null)
                span.SetStatus(ActivityStatusCode.Error, "Channel receive failed");
            else
                telemetry.Tracer.EnrichReceiveSpan(span, message);
            telemetry.Tracer.EndSpan(span);
        }

        Assert.True(telemetry.Flush());

        //Assert
        Assert.Equal(4, telemetry.Spans.Count);
        Assert.Equal(1, telemetry.CounterTotal("messaging.client.consumed.messages"));
        Assert.Equal(4, telemetry.HistogramCount("messaging.client.operation.duration"));
    }
}
