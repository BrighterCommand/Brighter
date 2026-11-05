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
using System.Collections.Generic;
using System.Linq;
using Paramore.Brighter.Core.Tests.Observability.StaleMessages.TestDoubles;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.StaleMessages;

public class StaleCountsCompletedTests
{
    private const string OUTBOX_TYPE = "InMemoryOutbox";
    private const string IMPLEMENTATION = "paramore.brighter.outbox.implementation";
    private const string DESTINATION = "messaging.destination.name";
    private const string COUNT = "paramore.brighter.outbox.stale.count";
    private const string OUTCOME = "paramore.brighter.outbox.stale.outcome";

    [Fact]
    public void When_both_counts_complete_should_report_each_gauge()
    {
        //Arrange
        using var observed = new ObservedStaleMessageInstruments();
        var readings = new OutboxStaleMessageReadings();
        _ = new OutboxStaleMessageMeter(observed.MeterFactory, readings);
        readings.Start(OUTBOX_TYPE, measuresTopics: true);

        //Act
        readings.CompleteOverall(2);
        readings.CompletePerTopic(new Dictionary<RoutingKey, int>
        {
            [new RoutingKey("orders")] = 2,
            [new RoutingKey("payments")] = 0
        });
        var measurements = observed.Observe();

        //Assert
        var overall = Assert.Single(measurements, m => m.Instrument == ObservedStaleMessageInstruments.MESSAGES);
        Assert.Equal(2, overall.Value);
        Assert.Equal([IMPLEMENTATION], overall.Attributes.Keys.ToArray());
        Assert.Equal(OUTBOX_TYPE, overall.Attributes[IMPLEMENTATION]);

        var topics = measurements.Where(m => m.Instrument == ObservedStaleMessageInstruments.TOPIC_MESSAGES).ToArray();
        Assert.All(topics, m => Assert.Equal([DESTINATION, IMPLEMENTATION], m.Attributes.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray()));
        Assert.All(topics, m => Assert.IsType<string>(m.Attributes[DESTINATION]));
        Assert.Equal(
            ["orders=2", "payments=0"],
            topics.Select(m => $"{m.Attributes[DESTINATION]}={m.Value}").OrderBy(s => s, StringComparer.Ordinal).ToArray());
        Assert.All(topics, m => Assert.Equal(OUTBOX_TYPE, m.Attributes[IMPLEMENTATION]));

        var completed = measurements
            .Where(m => m.Instrument == ObservedStaleMessageInstruments.MEASUREMENTS && Equals(m.Attributes[OUTCOME], "completed"))
            .ToArray();
        Assert.Equal(
            ["overall=1", "per_topic=1"],
            completed.Select(m => $"{m.Attributes[COUNT]}={m.Value}").OrderBy(s => s, StringComparer.Ordinal).ToArray());
    }
}
