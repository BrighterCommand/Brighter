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
using System.Threading.Tasks;
namespace Paramore.Brighter.Core.Tests.Observability.StaleMessages;

public class StaleReadingsInvalidWriteTests
{
    private const string OUTBOX_TYPE = "InMemoryOutbox";

    [Test]
    public async Task When_readings_receive_an_invalid_write_should_throw()
    {
        //Arrange
        var readings = new OutboxStaleMessageReadings();
        readings.Start(OUTBOX_TYPE, measuresTopics: true);
        readings.CompleteOverall(5);
        readings.CompletePerTopic(new Dictionary<RoutingKey, int> { [new RoutingKey("orders")] = 2 });

        var readingsWithoutTopics = new OutboxStaleMessageReadings();
        readingsWithoutTopics.Start(OUTBOX_TYPE, measuresTopics: false);

        //Act
        var negativeOverall = TestExceptionRecorder.Capture(() => readings.CompleteOverall(-1));
        var negativeTopic = TestExceptionRecorder.Capture(() => readings.CompletePerTopic(new Dictionary<RoutingKey, int>
        {
            [new RoutingKey("orders")] = 3,
            [new RoutingKey("payments")] = -1
        }));
        var skipBelowOne = TestExceptionRecorder.Capture(() => readings.Skip(StaleMessageCount.Overall, 0));
        var topicsNotMeasured = TestExceptionRecorder.Capture(() => readingsWithoutTopics.CompletePerTopic(
            new Dictionary<RoutingKey, int> { [new RoutingKey("orders")] = 1 }));

        //Assert
        await Assert.That(negativeOverall).IsTypeOf<ArgumentOutOfRangeException>();
        await Assert.That(negativeTopic).IsTypeOf<ArgumentOutOfRangeException>();
        await Assert.That(skipBelowOne).IsTypeOf<ArgumentOutOfRangeException>();
        await Assert.That(topicsNotMeasured).IsTypeOf<InvalidOperationException>();

        await Assert.That(readings.TryGetLatestOverall(out var overall)).IsTrue();
        await Assert.That(overall).IsEqualTo(5);
        await Assert.That(readings.TryGetLatestPerTopic(out var topics)).IsTrue();
        await Assert.That(topics.Select(t => $"{t.Key.Value}={t.Value}").ToArray()).IsEqualTo(["orders=2"]);
        await Assert.That(Totals(readings)).IsEqualTo([
                "Overall/Completed=1", "Overall/Failed=0", "Overall/Abandoned=0", "Overall/Skipped=0",
                "PerTopic/Completed=1", "PerTopic/Failed=0", "PerTopic/Abandoned=0", "PerTopic/Skipped=0"
            ]);

        await Assert.That(readingsWithoutTopics.TryGetLatestPerTopic(out _)).IsFalse();
        foreach (var total in Totals(readingsWithoutTopics))
{
    await Assert.That(total).EndsWith("=0");
}
    }

    private static string[] Totals(OutboxStaleMessageReadings readings) =>
        (from count in Enum.GetValues<StaleMessageCount>()
         from outcome in Enum.GetValues<StaleMeasurementOutcome>()
         select $"{count}/{outcome}={readings.GetTotal(count, outcome)}").ToArray();
}
