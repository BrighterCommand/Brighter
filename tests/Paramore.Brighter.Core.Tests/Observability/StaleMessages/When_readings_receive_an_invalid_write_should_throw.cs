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
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.StaleMessages;

public class StaleReadingsInvalidWriteTests
{
    private const string OUTBOX_TYPE = "InMemoryOutbox";

    [Fact]
    public void When_readings_receive_an_invalid_write_should_throw()
    {
        //Arrange
        var readings = new OutboxStaleMessageReadings();
        readings.Start(OUTBOX_TYPE, measuresTopics: true);
        readings.CompleteOverall(5);
        readings.CompletePerTopic(new Dictionary<RoutingKey, int> { [new RoutingKey("orders")] = 2 });

        var readingsWithoutTopics = new OutboxStaleMessageReadings();
        readingsWithoutTopics.Start(OUTBOX_TYPE, measuresTopics: false);

        //Act
        var negativeOverall = Record.Exception(() => readings.CompleteOverall(-1));
        var negativeTopic = Record.Exception(() => readings.CompletePerTopic(new Dictionary<RoutingKey, int>
        {
            [new RoutingKey("orders")] = 3,
            [new RoutingKey("payments")] = -1
        }));
        var skipBelowOne = Record.Exception(() => readings.Skip(StaleMessageCount.Overall, 0));
        var topicsNotMeasured = Record.Exception(() => readingsWithoutTopics.CompletePerTopic(
            new Dictionary<RoutingKey, int> { [new RoutingKey("orders")] = 1 }));

        //Assert
        Assert.IsType<ArgumentOutOfRangeException>(negativeOverall);
        Assert.IsType<ArgumentOutOfRangeException>(negativeTopic);
        Assert.IsType<ArgumentOutOfRangeException>(skipBelowOne);
        Assert.IsType<InvalidOperationException>(topicsNotMeasured);

        Assert.True(readings.TryGetLatestOverall(out var overall));
        Assert.Equal(5, overall);
        Assert.True(readings.TryGetLatestPerTopic(out var topics));
        Assert.Equal(["orders=2"], topics.Select(t => $"{t.Key.Value}={t.Value}").ToArray());
        Assert.Equal(
            [
                "Overall/Completed=1", "Overall/Failed=0", "Overall/Abandoned=0", "Overall/Skipped=0",
                "PerTopic/Completed=1", "PerTopic/Failed=0", "PerTopic/Abandoned=0", "PerTopic/Skipped=0"
            ],
            Totals(readings));

        Assert.False(readingsWithoutTopics.TryGetLatestPerTopic(out _));
        Assert.All(Totals(readingsWithoutTopics), total => Assert.EndsWith("=0", total));
    }

    private static string[] Totals(OutboxStaleMessageReadings readings) =>
        (from count in Enum.GetValues<StaleMessageCount>()
         from outcome in Enum.GetValues<StaleMeasurementOutcome>()
         select $"{count}/{outcome}={readings.GetTotal(count, outcome)}").ToArray();
}
