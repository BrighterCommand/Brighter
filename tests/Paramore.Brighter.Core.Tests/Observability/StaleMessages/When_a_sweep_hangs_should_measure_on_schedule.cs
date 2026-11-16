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
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.StaleMessages;

public class HungSweepTests
{
    private static readonly DateTimeOffset s_start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task When_a_sweep_hangs_should_measure_on_schedule()
    {
        //Arrange
        var disabled = await RunBesideAHungSweep(withTheFeature: false);

        //Act
        var enabled = await RunBesideAHungSweep(withTheFeature: true);

        //Assert
        Assert.True(disabled.SweepHung);
        Assert.True(enabled.SweepHung);
        Assert.All(enabled.Calls, call => Assert.False(call.Synchronous));
        Assert.Equal(
            Enumerable.Range(1, 5).Select(minutes => (DateTimeOffset?)s_start.AddMinutes(minutes)),
            enabled.Calls.Select(call => call.CalledAt));
        Assert.Equal(disabled.LockCalls, enabled.LockCalls);
        Assert.All(enabled.MeasuredInTime, Assert.True);
        Assert.Null(disabled.Stopping);
        Assert.Null(enabled.Stopping);
    }

    private static async Task<HungSweepRun> RunBesideAHungSweep(bool withTheFeature)
    {
        var clock = new TimerCountingTimeProvider(s_start);
        var outbox = new StaleMessageCountingOutbox(clock);
        var distributedLock = new RecordingDistributedLock(new InMemoryLock());
        using var metrics = new StaleMessageMetricsFixture(services =>
        {
            services.AddSingleton<TimeProvider>(clock);
            services.AddSingleton<IBrighterOptions>(new BrighterOptions());
            services.AddSingleton<IAmARequestContextFactory>(new InMemoryRequestContextFactory());
            var mappers = new ServiceCollectionMessageMapperRegistryBuilder(services);
            services.AddSingleton(mappers);
            var builder = new ServiceCollectionBrighterBuilder(
                services,
                new ServiceCollectionSubscriberRegistry(services),
                mappers);
            builder.AddProducers(configuration =>
            {
                configuration.ProducerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>());
                configuration.Outbox = outbox;
                configuration.DistributedLock = distributedLock;
            });
            builder.UseOutboxSweeper();
            if (withTheFeature)
                builder.UseOutboxStaleMessageMonitor(TimeSpan.FromMinutes(5), options =>
                    options.MeasurementInterval = TimeSpan.FromMinutes(1));
        });
        var hostedServices = metrics.Services.GetServices<IHostedService>().ToArray();
        using var sweeper = hostedServices.OfType<TimedOutboxSweeper>().Single();
        var monitor = hostedServices.OfType<TimedOutboxStaleMessageMonitor>().SingleOrDefault();

        bool sweepHung;
        var measuredInTime = new List<bool>();
        StaleCountCall[] calls;
        (string Operation, string Resource)[] lockCalls;
        try
        {
            await sweeper.StartAsync(default);
            sweepHung = SpinWait.SpinUntil(() => outbox.OutstandingMessagesCalls == 1, TimeSpan.FromSeconds(5));
            if (monitor is not null)
            {
                await monitor.StartAsync(default);
                measuredInTime.Add(SpinWait.SpinUntil(() => clock.TimersCreated >= 1, TimeSpan.FromSeconds(5)));
            }

            for (var interval = 1; interval <= 5; interval++)
            {
                clock.Advance(TimeSpan.FromMinutes(1));
                if (monitor is not null)
                    measuredInTime.Add(SpinWait.SpinUntil(
                        () => outbox.CountCalls.Count >= interval && clock.TimersCreated >= interval + 1,
                        TimeSpan.FromSeconds(5)));
            }

            calls = outbox.CountCalls.ToArray();
            lockCalls = distributedLock.Calls.ToArray();
        }
        finally
        {
            outbox.ReleaseOutstandingMessages();
        }

        var stopping = await Record.ExceptionAsync(() => sweeper.StopAsync(default).WaitAsync(TimeSpan.FromSeconds(5)));
        return new HungSweepRun(sweepHung, calls, lockCalls, measuredInTime, stopping);
    }

    private sealed record HungSweepRun(
        bool SweepHung,
        StaleCountCall[] Calls,
        (string Operation, string Resource)[] LockCalls,
        IReadOnlyList<bool> MeasuredInTime,
        Exception? Stopping);
}
