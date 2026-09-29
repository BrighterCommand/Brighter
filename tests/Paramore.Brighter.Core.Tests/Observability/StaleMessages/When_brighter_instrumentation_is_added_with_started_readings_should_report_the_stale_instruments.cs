#region Licence
/* The MIT License (MIT)
Copyright © 2026 Guilherme Appel

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

using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.Observability.StaleMessages.TestDoubles;
using Paramore.Brighter.Observability;
using System.Threading.Tasks;
namespace Paramore.Brighter.Core.Tests.Observability.StaleMessages;

public class StaleInstrumentationWithStartedReadingsTests
{
    private const string OUTBOX_TYPE = "InMemoryOutbox";
    private const string COUNT = "paramore.brighter.outbox.stale.count";
    private const string OUTCOME = "paramore.brighter.outbox.stale.outcome";

    [Test]
    public async Task When_brighter_instrumentation_is_added_with_started_readings_should_report_the_stale_instruments()
    {
        //Arrange
        var readings = new OutboxStaleMessageReadings();
        readings.Start(OUTBOX_TYPE, measuresTopics: true);

        //Act
        using var metrics = new StaleMessageMetricsFixture(services => services.AddSingleton(readings));
        var measurements = metrics.Observe();

        //Assert
        await Assert.That(metrics.PublishedInstruments).Contains(ObservedStaleMessageInstruments.MESSAGES);
        await Assert.That(metrics.PublishedInstruments).Contains(ObservedStaleMessageInstruments.TOPIC_MESSAGES);
        await Assert.That(metrics.PublishedInstruments).Contains(ObservedStaleMessageInstruments.MEASUREMENTS);
        var series = measurements.Where(m => m.Instrument == ObservedStaleMessageInstruments.MEASUREMENTS).ToArray();
        await Assert.That(series.Select(m => $"{m.Attributes[COUNT]}/{m.Attributes[OUTCOME]}").OrderBy(s => s, StringComparer.Ordinal).ToArray()).IsEqualTo([
                "overall/abandoned", "overall/completed", "overall/failed", "overall/skipped",
                "per_topic/abandoned", "per_topic/completed", "per_topic/failed", "per_topic/skipped"
            ]);
        foreach (var m in series)
{
    await Assert.That(m.Value).IsEqualTo(0);
}
    }
}
