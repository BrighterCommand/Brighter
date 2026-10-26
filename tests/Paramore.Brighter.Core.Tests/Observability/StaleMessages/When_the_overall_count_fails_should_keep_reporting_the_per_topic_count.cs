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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Core.Tests.Observability.StaleMessages.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.StaleMessages;

[Collection(StaleMessageLoggingCollection.Name)]
public class FailedOverallCountTests
{
    private const string COUNT = "paramore.brighter.outbox.stale.count";
    private const string OUTCOME = "paramore.brighter.outbox.stale.outcome";
    private const string DESTINATION = "messaging.destination.name";
    private static readonly DateTimeOffset s_start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly TimerCountingTimeProvider _clock = new(s_start);
    private readonly CapturingLoggerProvider _logs;

    public FailedOverallCountTests(StaleMessageLoggingFixture fixture)
    {
        _logs = fixture.Logs;
    }

    [Fact]
    public async Task When_the_overall_count_fails_should_keep_reporting_the_per_topic_count()
    {
        //Arrange
        _logs.Reset();
        var outbox = new StaleMessageTopicCountingOutbox(_clock);
        outbox.ReturnsByTopic(new Dictionary<RoutingKey, int>
        {
            { new RoutingKey("orders"), 2 },
            { new RoutingKey("payments"), 0 }
        });
        outbox.Throws(new InvalidOperationException("connection refused"));
        outbox.Throws(new InvalidOperationException("connection refused"));
        outbox.Throws(new InvalidOperationException("connection refused"));
        using var metrics = new StaleMessageMetricsFixture(services =>
        {
            services.AddSingleton<TimeProvider>(_clock);
            var builder = new ServiceCollectionBrighterBuilder(
                services,
                new ServiceCollectionSubscriberRegistry(services),
                new ServiceCollectionMessageMapperRegistryBuilder(services));
            builder.AddProducers(configuration =>
            {
                configuration.ProducerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
                {
                    { new RoutingKey("orders"), Producer("orders") },
                    { new RoutingKey("payments"), Producer("payments") }
                });
                configuration.Outbox = outbox;
            });
            builder.UseOutboxStaleMessageMonitor(TimeSpan.FromMinutes(5), options =>
                options.MeasurementInterval = TimeSpan.FromMinutes(1));
        });
        using var monitor = metrics.Services.GetServices<IHostedService>().OfType<TimedOutboxStaleMessageMonitor>().Single();
        var readings = metrics.Services.GetRequiredService<OutboxStaleMessageReadings>();
        await monitor.StartAsync(default);
        WaitUntilTheMonitorWaits(1);
        _clock.Advance(TimeSpan.FromSeconds(59));

        var dueTimes = new[] { s_start.AddMinutes(1), s_start.AddMinutes(2), s_start.AddMinutes(3) };
        var end = dueTimes[^1].AddSeconds(30);
        var now = s_start.AddSeconds(59);
        DateTimeOffset? heldAt = null;
        var collected = new List<StaleMeasurement>();
        IReadOnlyList<StaleMeasurement> afterTheFirstDueTime = [];
        IReadOnlyList<StaleMeasurement> afterTheLastDueTime = [];

        //Act
        while (now < end)
        {
            now = now.AddSeconds(1);
            if (!AdvanceReturns(TimeSpan.FromSeconds(1)))
            {
                heldAt = now;
                break;
            }

            var measured = Array.IndexOf(dueTimes, now) + 1;
            if (measured > 0)
            {
                Assert.True(SpinWait.SpinUntil(
                    () => readings.GetTotal(StaleMessageCount.Overall, StaleMeasurementOutcome.Failed) >= measured
                        && readings.GetTotal(StaleMessageCount.PerTopic, StaleMeasurementOutcome.Completed) >= measured,
                    TimeSpan.FromSeconds(5)));
                WaitUntilTheMonitorWaits(measured + 1);
            }

            var observed = metrics.Observe();
            collected.AddRange(observed);
            if (now == dueTimes[0].AddSeconds(20))
                afterTheFirstDueTime = observed;
            if (now == dueTimes[^1].AddSeconds(20))
                afterTheLastDueTime = observed;
        }

        //Assert
        Assert.Null(heldAt);
        Assert.Equal(dueTimes.Select(time => (DateTimeOffset?)time), outbox.TopicCountCalls.Select(call => call.CalledAt));
        var topics = collected
            .Where(m => m.Instrument == ObservedStaleMessageInstruments.TOPIC_MESSAGES)
            .Select(m => $"{m.Attributes[DESTINATION]}={m.Value}")
            .Distinct()
            .OrderBy(topic => topic, StringComparer.Ordinal);
        Assert.Equal(["orders=2", "payments=0"], topics);

        Assert.Equal(0, Completed(afterTheLastDueTime, "overall"));
        Assert.True(Completed(afterTheLastDueTime, "per_topic") > Completed(afterTheFirstDueTime, "per_topic"));

        var warnings = _logs.Entries.Where(entry => entry.LogLevel == LogLevel.Warning).ToList();
        Assert.Equal(3, warnings.Count);
        Assert.All(warnings, warning => Assert.Contains("Overall", warning.Message));
    }

    private static long Completed(IEnumerable<StaleMeasurement> measurements, string count) =>
        measurements
            .Where(m => m.Instrument == ObservedStaleMessageInstruments.MEASUREMENTS
                && Equals(m.Attributes[COUNT], count)
                && Equals(m.Attributes[OUTCOME], "completed"))
            .Sum(m => m.Value);

    private static InMemoryMessageProducer Producer(string topic) =>
        new(new InternalBus(), new Publication { Topic = new RoutingKey(topic) });

    private bool AdvanceReturns(TimeSpan step)
    {
        var advance = Task.Run(() => _clock.Advance(step));
        return SpinWait.SpinUntil(() => advance.IsCompleted, TimeSpan.FromSeconds(5));
    }

    private void WaitUntilTheMonitorWaits(int timers) =>
        Assert.True(SpinWait.SpinUntil(() => _clock.TimersCreated >= timers, TimeSpan.FromSeconds(5)));
}
