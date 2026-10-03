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
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.Observability.TestDoubles;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.Metrics;

public class SentMessageMetricsTests
{
    [Fact]
    public void When_publishing_messages_should_count_only_broker_send_attempts()
    {
        //Arrange
        using var telemetry = new InMemoryMetricsFromTraces();
        var publication = new Publication { Topic = new RoutingKey("sent-message-metrics") };
        var message = new Message(
            new MessageHeader(Id.Random(), publication.Topic, MessageType.MT_EVENT),
            new MessageBody("payload"));

        //Act
        for (var handler = 0; handler < 2; handler++)
        {
            var internalSpan = telemetry.Tracer.CreateSpan(CommandProcessorSpanOperation.Publish, new MyEvent());
            Assert.NotNull(internalSpan);
            telemetry.Tracer.EndSpan(internalSpan);
        }

        foreach (var failed in new[] { false, true })
        {
            var producerSpan = telemetry.Tracer.CreateProducerSpan(publication, message, parentActivity: null);
            Assert.NotNull(producerSpan);
            if (failed)
                producerSpan.SetStatus(ActivityStatusCode.Error, "Broker send failed");
            telemetry.Tracer.EndSpan(producerSpan);
        }

        Assert.True(telemetry.Flush());

        //Assert
        Assert.Equal(4, telemetry.Spans.Count);
        Assert.Equal(2, telemetry.CounterTotal("messaging.client.sent.messages"));
    }
}
