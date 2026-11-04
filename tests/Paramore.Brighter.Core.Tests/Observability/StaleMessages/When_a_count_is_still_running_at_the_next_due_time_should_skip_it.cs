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
public class CountStillRunningTests
{
    private readonly TimerCountingTimeProvider _clock = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly CapturingLoggerProvider _logs;

    public CountStillRunningTests(StaleMessageLoggingFixture fixture)
    {
        _logs = fixture.Logs;
    }

    [Fact]
    public async Task When_a_count_is_still_running_at_the_next_due_time_should_skip_it()
    {
        //Arrange
        _logs.Reset();
        var outbox = new StaleMessageCountingOutbox(_clock);
        outbox.BlockUntilReleased();
        outbox.Returns(3);
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
        Assert.True(SpinWait.SpinUntil(() => outbox.CountCalls.Count == 1, TimeSpan.FromSeconds(5)));
        var runningToken = outbox.CountCalls.Single().CancellationToken;
        var cancelledBeforeTheTimeout = runningToken.IsCancellationRequested;
        _clock.Advance(TimeSpan.FromSeconds(10));
        Assert.True(SpinWait.SpinUntil(() => runningToken.IsCancellationRequested, TimeSpan.FromSeconds(5)));
        Assert.True(SpinWait.SpinUntil(
            () => readings.GetTotal(StaleMessageCount.Overall, StaleMeasurementOutcome.Abandoned) >= 1,
            TimeSpan.FromSeconds(5)));
        WaitUntilTheMonitorWaits(2);

        var collected = new List<StaleMeasurement>();
        var whileRunning = new List<(long Completed, long Abandoned, long Skipped)>();
        void Collect()
        {
            collected.AddRange(metrics.Observe());
            whileRunning.Add((
                readings.GetTotal(StaleMessageCount.Overall, StaleMeasurementOutcome.Completed),
                readings.GetTotal(StaleMessageCount.Overall, StaleMeasurementOutcome.Abandoned),
                readings.GetTotal(StaleMessageCount.Overall, StaleMeasurementOutcome.Skipped)));
        }

        //Act
        _clock.Advance(TimeSpan.FromSeconds(50));
        WaitUntilTheMonitorWaits(3);
        _clock.Advance(TimeSpan.FromSeconds(20));
        Collect();
        _clock.Advance(TimeSpan.FromSeconds(40));
        WaitUntilTheMonitorWaits(4);
        _clock.Advance(TimeSpan.FromSeconds(20));
        Collect();
        _clock.Advance(TimeSpan.FromSeconds(10));
        outbox.Release(7);
        SpinWait.SpinUntil(() => outbox.CallEnded.IsCompleted, TimeSpan.FromSeconds(5));
        _clock.Advance(TimeSpan.FromSeconds(30));
        SpinWait.SpinUntil(
            () => readings.GetTotal(StaleMessageCount.Overall, StaleMeasurementOutcome.Completed) >= 1,
            TimeSpan.FromSeconds(5));
        var afterTheReturn = metrics.Observe();

        //Assert
        Assert.False(cancelledBeforeTheTimeout);
        Assert.Equal(
            [new DateTimeOffset(2026, 1, 1, 12, 1, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 1, 12, 4, 0, TimeSpan.Zero)],
            outbox.CountCalls.Select(call => call.CalledAt));
        var skipWarnings = _logs.Entries
            .Where(entry => entry.LogLevel == LogLevel.Warning && entry.Message.Contains("skipped"))
            .Select(entry => entry.Message)
            .ToArray();
        Assert.Equal(2, skipWarnings.Length);
        Assert.All(skipWarnings, message =>
            Assert.Contains("skipped 1 due times of the Overall count for StaleMessageCountingOutbox", message));
        Assert.All(whileRunning, totals =>
        {
            Assert.Equal(0, totals.Completed);
            Assert.True(totals.Abandoned > 0);
            Assert.True(totals.Skipped > 0);
        });
        Assert.DoesNotContain(collected, m => m.Instrument == ObservedStaleMessageInstruments.MESSAGES && m.Value == 7);
        var overall = Assert.Single(afterTheReturn, m => m.Instrument == ObservedStaleMessageInstruments.MESSAGES);
        Assert.Equal(3, overall.Value);
    }

    private static InMemoryMessageProducer Producer(string topic) =>
        new(new InternalBus(), new Publication { Topic = new RoutingKey(topic) });

    private void WaitUntilTheMonitorWaits(int timers) =>
        Assert.True(SpinWait.SpinUntil(() => _clock.TimersCreated >= timers, TimeSpan.FromSeconds(5)));
}
