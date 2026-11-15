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

using Paramore.Brighter.Core.Tests.Observability.TestDoubles;
using Paramore.Brighter.Observability;
using System.Threading.Tasks;
namespace Paramore.Brighter.Core.Tests.Observability.Metrics;

public class MetricsRegistrationOrderTests
{
    [Test]
    [Arguments(true, false)]
    [Arguments(false, false)]
    [Arguments(false, true)]
    public async Task When_registering_tracing_before_metrics_should_export_messaging_and_database_metrics(
        bool tracingFirst, bool combinedInstrumentation)
    {
        //Arrange
        using var telemetry = new InMemoryMetricsFromTraces(tracingFirst, combinedInstrumentation);
        var publication = new Publication { Topic = new RoutingKey("registration-order-metrics") };
        var message = new Message(
            new MessageHeader(Id.Random(), publication.Topic, MessageType.MT_EVENT),
            new MessageBody("payload"));

        //Act
        var producerSpan = telemetry.Tracer.CreateProducerSpan(publication, message, parentActivity: null);
        await Assert.That(producerSpan).IsNotNull();
        telemetry.Tracer.EndSpan(producerSpan);

        var receiveSpan = telemetry.Tracer.CreateReceiveSpan(publication.Topic, MessagingSystem.InternalBus);
        await Assert.That(receiveSpan).IsNotNull();
        telemetry.Tracer.EnrichReceiveSpan(receiveSpan, message);
        telemetry.Tracer.EndSpan(receiveSpan);

        var dbSpan = telemetry.Tracer.CreateDbSpan(
            new BoxSpanInfo(DbSystem.MySql, "metrics-test", BoxDbOperation.Add, "outbox"),
            parentActivity: null,
            options: InstrumentationOptions.All);
        await Assert.That(dbSpan).IsNotNull();
        telemetry.Tracer.EndSpan(dbSpan);

        await Assert.That(telemetry.Flush()).IsTrue();

        //Assert
        await Assert.That(telemetry.Spans.Count).IsEqualTo(3);
        await Assert.That(telemetry.CounterTotal("messaging.client.sent.messages")).IsEqualTo(1);
        await Assert.That(telemetry.CounterTotal("messaging.client.consumed.messages")).IsEqualTo(1);
        await Assert.That(telemetry.HistogramCount("db.client.operation.duration")).IsEqualTo(1);
    }
}
