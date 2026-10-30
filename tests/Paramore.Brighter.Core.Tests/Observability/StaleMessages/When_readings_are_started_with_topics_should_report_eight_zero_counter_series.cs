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
using Paramore.Brighter.Core.Tests.Observability.StaleMessages.TestDoubles;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.StaleMessages;

public class StaleReadingsStartedWithTopicsTests
{
    private const string OUTBOX_TYPE = "InMemoryOutbox";
    private const string IMPLEMENTATION = "paramore.brighter.outbox.implementation";
    private const string COUNT = "paramore.brighter.outbox.stale.count";
    private const string OUTCOME = "paramore.brighter.outbox.stale.outcome";

    [Fact]
    public void When_readings_are_started_with_topics_should_report_eight_zero_counter_series()
    {
        //Arrange
        using var observed = new ObservedStaleMessageInstruments();
        var readings = new OutboxStaleMessageReadings();
        _ = new OutboxStaleMessageMeter(observed.MeterFactory, readings);

        //Act
        readings.Start(OUTBOX_TYPE, measuresTopics: true);
        var measurements = observed.Observe();

        //Assert
        var series = measurements.Where(m => m.Instrument == ObservedStaleMessageInstruments.MEASUREMENTS).ToArray();
        Assert.All(series, m => Assert.Equal([IMPLEMENTATION, COUNT, OUTCOME], m.Attributes.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray()));
        Assert.Equal(
            [
                "overall/abandoned", "overall/completed", "overall/failed", "overall/skipped",
                "per_topic/abandoned", "per_topic/completed", "per_topic/failed", "per_topic/skipped"
            ],
            series.Select(m => $"{m.Attributes[COUNT]}/{m.Attributes[OUTCOME]}").OrderBy(s => s, StringComparer.Ordinal).ToArray());
        Assert.All(series, m => Assert.Equal(0, m.Value));
        Assert.All(series, m => Assert.Equal(OUTBOX_TYPE, m.Attributes[IMPLEMENTATION]));
        Assert.Equal("{measurement}", observed.Units[ObservedStaleMessageInstruments.MEASUREMENTS]);
        Assert.DoesNotContain(measurements, m => m.Instrument != ObservedStaleMessageInstruments.MEASUREMENTS);
    }
}
