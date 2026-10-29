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
public class CountStillRunningMeasurementsTests
{
    private const string COUNT = "paramore.brighter.outbox.stale.count";
    private const string OUTCOME = "paramore.brighter.outbox.stale.outcome";
    private static readonly DateTimeOffset s_start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task When_a_count_is_still_running_should_report_measurements_unlike_a_host_without_the_monitor()
    {
        //Act
        var enabled = await CollectWithTheMonitorAsync();
        var disabled = await CollectWithoutTheMonitorAsync();

        //Assert
        Assert.Equal(2, enabled.Count);
        Assert.All(enabled, observed =>
        {
            Assert.Equal(0, Overall(observed, "completed"));
            Assert.True(Overall(observed, "abandoned") > 0);
            Assert.True(Overall(observed, "skipped") > 0);
        });
        Assert.Equal(2, disabled.Count);
        Assert.All(disabled, observed =>
            Assert.DoesNotContain(observed, m => m.Instrument == ObservedStaleMessageInstruments.MEASUREMENTS));
    }

    private static async Task<IReadOnlyList<IReadOnlyList<StaleMeasurement>>> CollectWithTheMonitorAsync()
    {
        var clock = new TimerCountingTimeProvider(s_start);
        var outbox = new StaleMessageCountingOutbox(clock);
        outbox.BlockUntilReleased();
        outbox.Returns(3);
        using var metrics = new StaleMessageMetricsFixture(services =>
        {
            AddProducer(services, clock, outbox).UseOutboxStaleMessageMonitor(TimeSpan.FromMinutes(5), options =>
                options.MeasurementInterval = TimeSpan.FromMinutes(1));
        });
        using var monitor = metrics.Services.GetServices<IHostedService>().OfType<TimedOutboxStaleMessageMonitor>().Single();
        var readings = metrics.Services.GetRequiredService<OutboxStaleMessageReadings>();
        await monitor.StartAsync(default);
        WaitUntilTheMonitorWaits(clock, 1);
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True(SpinWait.SpinUntil(() => outbox.CountCalls.Count == 1, TimeSpan.FromSeconds(5)));
        var runningToken = outbox.CountCalls.Single().CancellationToken;
        clock.Advance(TimeSpan.FromSeconds(10));
        Assert.True(SpinWait.SpinUntil(() => runningToken.IsCancellationRequested, TimeSpan.FromSeconds(5)));
        Assert.True(SpinWait.SpinUntil(
            () => readings.GetTotal(StaleMessageCount.Overall, StaleMeasurementOutcome.Abandoned) >= 1,
            TimeSpan.FromSeconds(5)));
        WaitUntilTheMonitorWaits(clock, 2);

        var collected = new List<IReadOnlyList<StaleMeasurement>>();
        clock.Advance(TimeSpan.FromSeconds(50));
        WaitUntilTheMonitorWaits(clock, 3);
        clock.Advance(TimeSpan.FromSeconds(20));
        collected.Add(metrics.Observe());
        clock.Advance(TimeSpan.FromSeconds(40));
        WaitUntilTheMonitorWaits(clock, 4);
        clock.Advance(TimeSpan.FromSeconds(20));
        collected.Add(metrics.Observe());
        clock.Advance(TimeSpan.FromSeconds(10));
        outbox.Release(7);
        SpinWait.SpinUntil(() => outbox.CallEnded.IsCompleted, TimeSpan.FromSeconds(5));

        return collected;
    }

    private static async Task<IReadOnlyList<IReadOnlyList<StaleMeasurement>>> CollectWithoutTheMonitorAsync()
    {
        var clock = new TimerCountingTimeProvider(s_start);
        var outbox = new StaleMessageCountingOutbox(clock);
        outbox.Returns(3);
        using var metrics = new StaleMessageMetricsFixture(services => AddProducer(services, clock, outbox));
        var hostedServices = metrics.Services.GetServices<IHostedService>().ToList();
        foreach (var hostedService in hostedServices)
            await hostedService.StartAsync(default);

        var collected = new List<IReadOnlyList<StaleMeasurement>>();
        clock.Advance(new TimeSpan(0, 2, 20));
        collected.Add(metrics.Observe());
        clock.Advance(TimeSpan.FromMinutes(1));
        collected.Add(metrics.Observe());
        foreach (var hostedService in hostedServices)
            await hostedService.StopAsync(default);

        return collected;
    }

    private static ServiceCollectionBrighterBuilder AddProducer(
        IServiceCollection services, TimeProvider clock, StaleMessageCountingOutbox outbox)
    {
        services.AddSingleton(clock);
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
        return builder;
    }

    private static long Overall(IEnumerable<StaleMeasurement> measurements, string outcome) =>
        measurements
            .Where(m => m.Instrument == ObservedStaleMessageInstruments.MEASUREMENTS
                && Equals(m.Attributes[COUNT], "overall")
                && Equals(m.Attributes[OUTCOME], outcome))
            .Sum(m => m.Value);

    private static InMemoryMessageProducer Producer(string topic) =>
        new(new InternalBus(), new Publication { Topic = new RoutingKey(topic) });

    private static void WaitUntilTheMonitorWaits(TimerCountingTimeProvider clock, int timers) =>
        Assert.True(SpinWait.SpinUntil(() => clock.TimersCreated >= timers, TimeSpan.FromSeconds(5)));
}
