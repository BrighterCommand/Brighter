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
using Paramore.Brighter.Core.Tests.Observability.StaleMessages.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.StaleMessages;

public class BlockedPerTopicCountTests
{
    private const string COUNT = "paramore.brighter.outbox.stale.count";
    private const string OUTCOME = "paramore.brighter.outbox.stale.outcome";
    private static readonly DateTimeOffset s_start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly TimerCountingTimeProvider _clock = new(s_start);

    [Fact]
    public async Task When_the_per_topic_count_is_blocked_should_keep_measuring_the_overall_count()
    {
        //Arrange
        var outbox = new StaleMessageTopicCountingOutbox(_clock);
        outbox.Returns(1);
        outbox.BlockTopicCountUntilReleased();
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

        var blockedAt = s_start.AddMinutes(1);
        var deadline = blockedAt.AddSeconds(10);
        var releasedAt = s_start.AddMinutes(3).AddSeconds(30);
        var nextTopicCallAt = s_start.AddMinutes(4);
        var end = nextTopicCallAt.AddSeconds(30);
        var now = s_start.AddSeconds(59);
        var dueTimes = 0;
        DateTimeOffset? heldAt = null;
        DateTimeOffset? cancelledAt = null;
        var overallByDueTime = new List<IReadOnlyList<long>>();
        var topicsBeforeTheNextTopicCall = new List<StaleMeasurement>();
        var whileBlocked = new List<(long PerTopicCompleted, long OverallCompleted)>();

        //Act
        while (now < end)
        {
            now = now.AddSeconds(1);
            if (!AdvanceReturns(TimeSpan.FromSeconds(1)))
            {
                heldAt = now;
                break;
            }

            if (now.Second == 0)
            {
                dueTimes++;
                SpinWait.SpinUntil(
                    () => readings.GetTotal(StaleMessageCount.Overall, StaleMeasurementOutcome.Completed) >= dueTimes,
                    TimeSpan.FromSeconds(5));
            }
            if (now == deadline)
            {
                SpinWait.SpinUntil(() => IsTopicCountCancelled(outbox), TimeSpan.FromSeconds(5));
                SpinWait.SpinUntil(
                    () => readings.GetTotal(StaleMessageCount.PerTopic, StaleMeasurementOutcome.Abandoned) >= 1,
                    TimeSpan.FromSeconds(5));
            }
            if (now == releasedAt)
            {
                outbox.ReleaseTopicCount(new Dictionary<RoutingKey, int>
                {
                    { new RoutingKey("orders"), 4 },
                    { new RoutingKey("payments"), 4 }
                });
                SpinWait.SpinUntil(() => outbox.TopicCallEnded.IsCompleted, TimeSpan.FromSeconds(5));
            }
            if (now == nextTopicCallAt)
                SpinWait.SpinUntil(
                    () => readings.GetTotal(StaleMessageCount.PerTopic, StaleMeasurementOutcome.Completed) >= 1,
                    TimeSpan.FromSeconds(5));

            if (cancelledAt is null && IsTopicCountCancelled(outbox))
                cancelledAt = now;
            var observed = metrics.Observe();
            if (now < nextTopicCallAt)
                topicsBeforeTheNextTopicCall.AddRange(observed.Where(m => m.Instrument == ObservedStaleMessageInstruments.TOPIC_MESSAGES));
            if (now.Second == 20)
                overallByDueTime.Add(observed
                    .Where(m => m.Instrument == ObservedStaleMessageInstruments.MESSAGES)
                    .Select(m => m.Value)
                    .ToList());
            if (now == s_start.AddMinutes(2).AddSeconds(20) || now == s_start.AddMinutes(3).AddSeconds(20))
                whileBlocked.Add((Completed(observed, "per_topic"), Completed(observed, "overall")));
        }

        //Assert
        Assert.Null(heldAt);
        Assert.Equal(4, overallByDueTime.Count);
        Assert.All(overallByDueTime, values => Assert.Equal([1], values));
        Assert.Equal(deadline, cancelledAt);
        Assert.Empty(topicsBeforeTheNextTopicCall);
        Assert.Equal([blockedAt, nextTopicCallAt], outbox.TopicCountCalls.Select(call => call.CalledAt));
        Assert.Equal(2, whileBlocked.Count);
        Assert.All(whileBlocked, totals => Assert.Equal(0, totals.PerTopicCompleted));
        Assert.True(whileBlocked[1].OverallCompleted > whileBlocked[0].OverallCompleted);
    }

    private static long Completed(IEnumerable<StaleMeasurement> measurements, string count) =>
        measurements
            .Where(m => m.Instrument == ObservedStaleMessageInstruments.MEASUREMENTS
                && Equals(m.Attributes[COUNT], count)
                && Equals(m.Attributes[OUTCOME], "completed"))
            .Sum(m => m.Value);

    private static InMemoryMessageProducer Producer(string topic) =>
        new(new InternalBus(), new Publication { Topic = new RoutingKey(topic) });

    private static bool IsTopicCountCancelled(StaleMessageTopicCountingOutbox outbox) =>
        outbox.TopicCountCalls.Any(call => call.CancellationToken.IsCancellationRequested);

    private bool AdvanceReturns(TimeSpan step)
    {
        var advance = Task.Run(() => _clock.Advance(step));
        return SpinWait.SpinUntil(() => advance.IsCompleted, TimeSpan.FromSeconds(5));
    }

    private void WaitUntilTheMonitorWaits(int timers) =>
        Assert.True(SpinWait.SpinUntil(() => _clock.TimersCreated >= timers, TimeSpan.FromSeconds(5)));
}
