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

[Collection(StaleMessageLoggingCollection.Name)]
public class RepeatedFailureTests
{
    private const string COUNT = "paramore.brighter.outbox.stale.count";
    private const string OUTCOME = "paramore.brighter.outbox.stale.outcome";
    private static readonly DateTimeOffset s_start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task When_counts_keep_failing_should_differ_from_counts_of_zero()
    {
        //Arrange
        var connectionRefused = new InvalidOperationException("connection refused");

        //Act
        var failing = await CollectAsync(outbox =>
        {
            outbox.Throws(connectionRefused);
            outbox.Throws(connectionRefused);
        });
        var zero = await CollectAsync(_ => { });

        //Assert
        Assert.Equal(zero.Select(totals => totals.At), failing.Select(totals => totals.At));
        Assert.All(failing.Zip(zero), pair => Assert.NotEqual(pair.Second.Completed, pair.First.Completed));
        Assert.All(failing.Zip(zero), pair => Assert.NotEqual(pair.Second.Failed, pair.First.Failed));
        Assert.All(failing.SelectMany(totals => totals.Gauge), value => Assert.Equal(0, value));
        Assert.All(zero.SelectMany(totals => totals.Gauge), value => Assert.Equal(0, value));
    }

    private static async Task<IReadOnlyList<CollectedTotals>> CollectAsync(Action<StaleMessageCountingOutbox> afterTheFirstCount)
    {
        var clock = new TimerCountingTimeProvider(s_start);
        var outbox = new StaleMessageCountingOutbox(clock);
        outbox.Returns(0);
        using var metrics = new StaleMessageMetricsFixture(services =>
        {
            services.AddSingleton<TimeProvider>(clock);
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
        Assert.True(SpinWait.SpinUntil(() => clock.TimersCreated >= 1, TimeSpan.FromSeconds(5)));
        clock.Advance(TimeSpan.FromSeconds(59));

        var firstCollection = s_start.AddMinutes(2).AddSeconds(10);
        var end = s_start.AddMinutes(3).AddSeconds(30);
        var now = s_start.AddSeconds(59);
        var dueTimes = 0;
        var collected = new List<CollectedTotals>();

        while (now < end)
        {
            now = now.AddSeconds(1);
            Assert.True(AdvanceReturns(clock, TimeSpan.FromSeconds(1)), $"the clock was held at {now:HH:mm:ss}");

            if (now.Second == 0)
            {
                dueTimes++;
                SpinWait.SpinUntil(() => Ended(readings) >= dueTimes, TimeSpan.FromSeconds(5));
                if (dueTimes == 1)
                    afterTheFirstCount(outbox);
            }

            var observed = metrics.Observe();
            if (now >= firstCollection && now.Second % 10 == 0)
                collected.Add(new CollectedTotals(now, Overall(observed, "completed"), Overall(observed, "failed"), Gauge(observed)));
        }

        return collected;
    }

    private static long Ended(OutboxStaleMessageReadings readings) =>
        readings.GetTotal(StaleMessageCount.Overall, StaleMeasurementOutcome.Completed)
        + readings.GetTotal(StaleMessageCount.Overall, StaleMeasurementOutcome.Failed);

    private static long Overall(IEnumerable<StaleMeasurement> measurements, string outcome) =>
        measurements
            .Where(m => m.Instrument == ObservedStaleMessageInstruments.MEASUREMENTS
                && Equals(m.Attributes[COUNT], "overall")
                && Equals(m.Attributes[OUTCOME], outcome))
            .Sum(m => m.Value);

    private static IReadOnlyList<long> Gauge(IEnumerable<StaleMeasurement> measurements) =>
        measurements
            .Where(m => m.Instrument == ObservedStaleMessageInstruments.MESSAGES)
            .Select(m => m.Value)
            .ToList();

    private static InMemoryMessageProducer Producer(string topic) =>
        new(new InternalBus(), new Publication { Topic = new RoutingKey(topic) });

    private static bool AdvanceReturns(TimerCountingTimeProvider clock, TimeSpan step)
    {
        var advance = Task.Run(() => clock.Advance(step));
        return SpinWait.SpinUntil(() => advance.IsCompleted, TimeSpan.FromSeconds(5));
    }

    private sealed record CollectedTotals(DateTimeOffset At, long Completed, long Failed, IReadOnlyList<long> Gauge);
}
