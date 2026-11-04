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

public class StaleOverallCountSkippedTests
{
    private const string COUNT = "paramore.brighter.outbox.stale.count";
    private const string OUTCOME = "paramore.brighter.outbox.stale.outcome";

    [Fact]
    public void When_the_overall_count_skips_three_due_times_should_count_three_skips()
    {
        //Arrange
        using var observed = new ObservedStaleMessageInstruments();
        var readings = new OutboxStaleMessageReadings();
        _ = new OutboxStaleMessageMeter(observed.MeterFactory, readings);
        readings.Start("InMemoryOutbox", measuresTopics: true);
        readings.CompleteOverall(5);

        //Act
        readings.Skip(StaleMessageCount.Overall, 3);
        var measurements = observed.Observe();

        //Assert
        Assert.DoesNotContain(measurements, m => m.Instrument == ObservedStaleMessageInstruments.MESSAGES);

        Assert.Equal(
            [
                "overall/abandoned=0", "overall/completed=1", "overall/failed=0", "overall/skipped=3",
                "per_topic/abandoned=0", "per_topic/completed=0", "per_topic/failed=0", "per_topic/skipped=0"
            ],
            measurements
                .Where(m => m.Instrument == ObservedStaleMessageInstruments.MEASUREMENTS)
                .Select(m => $"{m.Attributes[COUNT]}/{m.Attributes[OUTCOME]}={m.Value}")
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToArray());
    }
}
