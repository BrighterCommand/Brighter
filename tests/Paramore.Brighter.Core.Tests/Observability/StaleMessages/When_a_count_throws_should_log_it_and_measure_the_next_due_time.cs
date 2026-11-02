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
public class FailedCountTests
{
    private const string COUNT = "paramore.brighter.outbox.stale.count";
    private const string OUTCOME = "paramore.brighter.outbox.stale.outcome";
    private static readonly DateTimeOffset s_start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly TimerCountingTimeProvider _clock = new(s_start);
    private readonly CapturingLoggerProvider _logs;

    public FailedCountTests(StaleMessageLoggingFixture fixture)
    {
        _logs = fixture.Logs;
    }

    [Fact]
    public async Task When_a_count_throws_should_log_it_and_measure_the_next_due_time()
    {
        //Arrange
        _logs.Reset();
        var connectionRefused = new InvalidOperationException("connection refused");
        var outbox = new StaleMessageCountingOutbox(_clock);
        outbox.Returns(2);
        outbox.Throws(connectionRefused);
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
        _clock.Advance(TimeSpan.FromSeconds(59));

        var failedAt = s_start.AddMinutes(1);
        var measuredAt = s_start.AddMinutes(2);
        var end = measuredAt.AddSeconds(30);
        var now = s_start.AddSeconds(59);
        DateTimeOffset? heldAt = null;
        var beforeTheNextDueTime = new List<StaleMeasurement>();
        var fromTheNextDueTime = new List<StaleMeasurement>();
        IReadOnlyList<StaleMeasurement> afterTheFailure = [];
        IReadOnlyList<StaleMeasurement> afterTheMeasurement = [];

        //Act
        while (now < end)
        {
            now = now.AddSeconds(1);
            if (!AdvanceReturns(TimeSpan.FromSeconds(1)))
            {
                heldAt = now;
                break;
            }

            if (now == failedAt)
                SpinWait.SpinUntil(
                    () => readings.GetTotal(StaleMessageCount.Overall, StaleMeasurementOutcome.Failed) >= 1,
                    TimeSpan.FromSeconds(5));
            if (now == measuredAt)
                SpinWait.SpinUntil(
                    () => readings.GetTotal(StaleMessageCount.Overall, StaleMeasurementOutcome.Completed) >= 1,
                    TimeSpan.FromSeconds(5));

            var observed = metrics.Observe();
            if (now < measuredAt)
                beforeTheNextDueTime.AddRange(observed);
            else
                fromTheNextDueTime.AddRange(observed);
            if (now == failedAt.AddSeconds(20))
                afterTheFailure = observed;
            if (now == measuredAt.AddSeconds(20))
                afterTheMeasurement = observed;
        }

        //Assert
        Assert.Null(heldAt);
        Assert.Equal([failedAt, measuredAt], outbox.CountCalls.Select(call => call.CalledAt));
        Assert.DoesNotContain(beforeTheNextDueTime, m => m.Instrument == ObservedStaleMessageInstruments.MESSAGES);
        var overall = fromTheNextDueTime
            .Where(m => m.Instrument == ObservedStaleMessageInstruments.MESSAGES)
            .Select(m => m.Value)
            .Distinct();
        Assert.Equal([2], overall);
        Assert.True(CompletedOverall(afterTheMeasurement) > CompletedOverall(afterTheFailure));

        var warning = Assert.Single(_logs.Entries, entry => entry.LogLevel == LogLevel.Warning);
        Assert.Same(connectionRefused, warning.Exception);
        Assert.Contains(nameof(StaleMessageCountingOutbox), warning.Message);
        Assert.Contains("Overall", warning.Message);
    }

    private static long CompletedOverall(IEnumerable<StaleMeasurement> measurements) =>
        measurements
            .Where(m => m.Instrument == ObservedStaleMessageInstruments.MEASUREMENTS
                && Equals(m.Attributes[COUNT], "overall")
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
