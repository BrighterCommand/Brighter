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

public class StaleReadingsStartedWithoutTopicsTests
{
    private const string COUNT = "paramore.brighter.outbox.stale.count";
    private const string OUTCOME = "paramore.brighter.outbox.stale.outcome";

    [Fact]
    public void When_readings_are_started_without_topics_should_report_only_the_overall_counter_series()
    {
        //Arrange
        using var observed = new ObservedStaleMessageInstruments();
        var readings = new OutboxStaleMessageReadings();
        _ = new OutboxStaleMessageMeter(observed.MeterFactory, readings);

        //Act
        readings.Start("InMemoryOutbox", measuresTopics: false);
        var measurements = observed.Observe();

        //Assert
        var series = measurements.Where(m => m.Instrument == ObservedStaleMessageInstruments.MEASUREMENTS).ToArray();
        Assert.Equal(
            ["overall/abandoned", "overall/completed", "overall/failed", "overall/skipped"],
            series.Select(m => $"{m.Attributes[COUNT]}/{m.Attributes[OUTCOME]}").OrderBy(s => s, StringComparer.Ordinal).ToArray());
        Assert.All(series, m => Assert.Equal(0, m.Value));
        Assert.DoesNotContain(measurements, m => m.Instrument != ObservedStaleMessageInstruments.MEASUREMENTS);
    }
}
