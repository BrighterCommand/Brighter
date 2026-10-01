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

using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
using Paramore.Brighter.Extensions.Diagnostics;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.Metrics;

public class TracingWithoutMetricsTests
{
    [Fact]
    public void When_registering_tracing_without_metrics_should_export_spans()
    {
        //Arrange
        var spans = new List<Activity>();
        var services = new ServiceCollection();
        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing.AddBrighterInstrumentation().AddInMemoryExporter(spans));
        using var provider = services.BuildServiceProvider();
        var tracing = provider.GetRequiredService<TracerProvider>();
        var tracer = provider.GetRequiredService<IAmABrighterTracer>();
        var publication = new Publication { Topic = new RoutingKey("tracing-without-metrics") };
        var message = new Message(
            new MessageHeader(Id.Random(), publication.Topic, MessageType.MT_EVENT), new MessageBody("payload"));

        //Act
        var span = tracer.CreateProducerSpan(publication, message, parentActivity: null);
        Assert.NotNull(span);
        tracer.EndSpan(span);
        Assert.True(tracing.ForceFlush());

        //Assert
        Assert.Single(spans);
    }
}
