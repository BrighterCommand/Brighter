#region Licence

/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.Metrics;

public class NoSweepInProgressZeroRunningTimeGaugeTests
{
    [Fact]
    public async Task When_no_sweep_is_in_progress_should_report_a_zero_running_time_gauge_before_the_first_sweep()
    {
        //Arrange
        var timeProvider = new FakeTimeProvider();
        var state = new OutboxSweeperState(timeProvider);
        using var outbox = new SweepableOutbox();
        using var metrics = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var meterFactory = metrics.GetRequiredService<IMeterFactory>();
        var meter = new SweeperMeter(meterFactory, state);
        using var recorded = new RunningTimeListener(meterFactory);
        using var sweeper = new TimedOutboxSweeper(
            outbox.ScopeFactory,
            new InMemoryLock(),
            new TimedOutboxSweeperOptions { TimerInterval = 60, MinimumMessageAge = TimeSpan.Zero },
            timeProvider: timeProvider,
            state: state);
        timeProvider.Advance(TimeSpan.FromSeconds(30));

        //Act
        await Task.Run(recorded.Observe).WaitAsync(TimeSpan.FromSeconds(1));

        //Assert
        Assert.Equal([0.0], recorded.Values);
    }

    [Fact]
    public async Task When_no_sweep_is_in_progress_should_report_a_zero_running_time_gauge_after_a_completed_sweep()
    {
        //Arrange
        using var outbox = new SweepableOutbox();

        //Act
        var values = await ObserveHalfAnIntervalAfterTheFirstSweepEnds(outbox.ScopeFactory, new InMemoryLock(), SweepOutcome.Completed);

        //Assert
        Assert.Equal([0.0], values);
    }

    [Fact]
    public async Task When_no_sweep_is_in_progress_should_report_a_zero_running_time_gauge_after_a_failed_sweep()
    {
        //Arrange
        var services = new ServiceCollection();
        services.AddTransient<IAmAnOutboxProducerMediator>(_ => throw new InvalidOperationException("every sweep fails"));
        await using var provider = services.BuildServiceProvider();

        //Act
        var values = await ObserveHalfAnIntervalAfterTheFirstSweepEnds(
            provider.GetRequiredService<IServiceScopeFactory>(), new InMemoryLock(), SweepOutcome.Failed);

        //Assert
        Assert.Equal([0.0], values);
    }

    [Fact]
    public async Task When_no_sweep_is_in_progress_should_report_a_zero_running_time_gauge_after_a_lock_unavailable_sweep()
    {
        //Arrange
        using var outbox = new SweepableOutbox();

        //Act
        var values = await ObserveHalfAnIntervalAfterTheFirstSweepEnds(outbox.ScopeFactory, new HeldDistributedLock(), SweepOutcome.LockUnavailable);

        //Assert
        Assert.Equal([0.0], values);
    }

    [Fact]
    public async Task When_no_sweep_is_in_progress_should_report_a_zero_running_time_gauge_after_a_blocked_sweep_is_released()
    {
        //Arrange
        var timeProvider = new FakeTimeProvider();
        var state = new OutboxSweeperState(timeProvider);
        using var mediator = new GatedOutboxProducerMediator();
        var services = new ServiceCollection();
        services.AddSingleton<IAmAnOutboxProducerMediator>(mediator);
        using var provider = services.BuildServiceProvider();
        using var metrics = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var meterFactory = metrics.GetRequiredService<IMeterFactory>();
        var meter = new SweeperMeter(meterFactory, state);
        using var recorded = new RunningTimeListener(meterFactory);
        var sweeper = new TimedOutboxSweeper(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new InMemoryLock(),
            new TimedOutboxSweeperOptions { TimerInterval = 60, MinimumMessageAge = TimeSpan.Zero },
            timeProvider: timeProvider,
            state: state);

        try
        {
            await sweeper.StartAsync(CancellationToken.None);
            var reachedMediator = mediator.WaitForSweepToStart(TimeSpan.FromSeconds(10));
            var stopping = sweeper.StopAsync(CancellationToken.None);
            mediator.Open();
            await stopping.WaitAsync(TimeSpan.FromSeconds(10));
            timeProvider.Advance(TimeSpan.FromSeconds(30));

            //Act
            await Task.Run(recorded.Observe).WaitAsync(TimeSpan.FromSeconds(1));

            //Assert
            Assert.True(reachedMediator, "The sweep did not reach the mediator");
            Assert.False(state.Read().IsSweepInProgress);
            Assert.Equal([0.0], recorded.Values);
        }
        finally
        {
            mediator.Open();
            sweeper.Dispose();
        }
    }

    private static async Task<double[]> ObserveHalfAnIntervalAfterTheFirstSweepEnds(
        IServiceScopeFactory scopeFactory,
        IDistributedLock distributedLock,
        SweepOutcome expectedOutcome)
    {
        var timeProvider = new FakeTimeProvider();
        var state = new OutboxSweeperState(timeProvider);
        using var metrics = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var meterFactory = metrics.GetRequiredService<IMeterFactory>();
        var meter = new SweeperMeter(meterFactory, state);
        using var recorded = new RunningTimeListener(meterFactory);
        using var sweeper = new TimedOutboxSweeper(
            scopeFactory,
            distributedLock,
            new TimedOutboxSweeperOptions { TimerInterval = 60, MinimumMessageAge = TimeSpan.Zero },
            timeProvider: timeProvider,
            state: state);

        await sweeper.StartAsync(CancellationToken.None);
        try
        {
            await WaitForOutcome(state, expectedOutcome).WaitAsync(TimeSpan.FromSeconds(10));
            timeProvider.Advance(TimeSpan.FromSeconds(30));
            await Task.Run(recorded.Observe).WaitAsync(TimeSpan.FromSeconds(1));
            return [.. recorded.Values];
        }
        finally
        {
            await sweeper.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    private static async Task WaitForOutcome(OutboxSweeperState state, SweepOutcome expectedOutcome)
    {
        while (state.Read().LastOutcome != expectedOutcome) await Task.Delay(5);
    }
}
