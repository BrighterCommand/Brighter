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
public class StoppingDuringBothCountsTests
{
    private const string COUNT = "paramore.brighter.outbox.stale.count";
    private const string OUTCOME = "paramore.brighter.outbox.stale.outcome";
    private static readonly DateTimeOffset s_start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly TimerCountingTimeProvider _clock = new(s_start);
    private readonly CapturingLoggerProvider _logs;

    public StoppingDuringBothCountsTests(StaleMessageLoggingFixture fixture)
    {
        _logs = fixture.Logs;
    }

    [Fact]
    public async Task When_stopping_during_both_counts_should_record_no_outcome_and_clear_the_gauges()
    {
        //Arrange
        _logs.Reset();
        var outbox = new StaleMessageTopicCountingOutbox(_clock);
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
                    { new RoutingKey("orders"), Producer("orders") }
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
        _clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True(SpinWait.SpinUntil(
            () => readings.GetTotal(StaleMessageCount.Overall, StaleMeasurementOutcome.Completed) == 1
                && readings.GetTotal(StaleMessageCount.PerTopic, StaleMeasurementOutcome.Completed) == 1,
            TimeSpan.FromSeconds(5)));
        outbox.BlockUntilReleased();
        outbox.BlockTopicCountUntilReleased();
        WaitUntilTheMonitorWaits(2);
        _clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True(SpinWait.SpinUntil(
            () => outbox.CountCalls.Count == 2 && outbox.TopicCountCalls.Count == 2,
            TimeSpan.FromSeconds(5)));
        var beforeTheStop = metrics.Observe();

        bool gaugesCleared;
        bool waitsForTheTimeout;
        bool timeoutReturned;
        bool stopped;
        IReadOnlyList<StaleMeasurement> afterTheStop;

        //Act
        try
        {
            var stopping = monitor.StopAsync(CancellationToken.None);
            gaugesCleared = SpinWait.SpinUntil(() => !metrics.Observe().Any(IsAGauge), TimeSpan.FromSeconds(5));
            waitsForTheTimeout = SpinWait.SpinUntil(() => _clock.TimersCreated >= 4, TimeSpan.FromSeconds(5));
            timeoutReturned = AdvanceReturns(TimeSpan.FromSeconds(10));
            stopped = SpinWait.SpinUntil(() => stopping.IsCompleted, TimeSpan.FromSeconds(5));
            afterTheStop = metrics.Observe();
        }
        finally
        {
            outbox.Release(0);
            outbox.ReleaseTopicCount(new Dictionary<RoutingKey, int> { { new RoutingKey("orders"), 0 } });
        }

        //Assert
        Assert.Contains(beforeTheStop, m => m.Instrument == ObservedStaleMessageInstruments.MESSAGES);
        Assert.Contains(beforeTheStop, m => m.Instrument == ObservedStaleMessageInstruments.TOPIC_MESSAGES);
        Assert.True(gaugesCleared);
        Assert.True(waitsForTheTimeout);
        Assert.True(timeoutReturned);
        Assert.True(stopped);
        Assert.DoesNotContain(afterTheStop, IsAGauge);
        Assert.Equal(Totals(beforeTheStop), Totals(afterTheStop));
        Assert.DoesNotContain(_logs.Entries, entry => entry.LogLevel >= LogLevel.Warning);
    }

    private static bool IsAGauge(StaleMeasurement measurement) =>
        measurement.Instrument is ObservedStaleMessageInstruments.MESSAGES or ObservedStaleMessageInstruments.TOPIC_MESSAGES;

    private static IEnumerable<string> Totals(IEnumerable<StaleMeasurement> measurements) =>
        measurements
            .Where(m => m.Instrument == ObservedStaleMessageInstruments.MEASUREMENTS)
            .Select(m => $"{m.Attributes[COUNT]}/{m.Attributes[OUTCOME]}={m.Value}")
            .OrderBy(total => total, StringComparer.Ordinal);

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
