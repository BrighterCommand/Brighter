using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class SweeperAllocationTests
{
    private const int SweepsToRun = 10_000;
    private const int WindowSize = 100;
    private const int WarmUpSweeps = 100;
    private const int TimerIntervalSeconds = 5;

    [Fact]
    public async Task When_ten_thousand_sweeps_run_should_not_grow_the_per_sweep_allocation()
    {
        //Arrange
        var timeProvider = new FakeTimeProvider();
        using var outbox = new SweepableOutbox();
        var meter = new AllocationReadingSweeperMeter(SweepsToRun);
        var sweeper = new TimedOutboxSweeper(
            outbox.ScopeFactory,
            new InMemoryLock(),
            new TimedOutboxSweeperOptions { TimerInterval = TimerIntervalSeconds, MinimumMessageAge = TimeSpan.Zero },
            timeProvider: timeProvider,
            meter: meter);

        //Act
        await sweeper.StartAsync(CancellationToken.None);
        var allSwept = true;
        for (var sweep = 1; sweep <= SweepsToRun && allSwept; sweep++)
        {
            if (sweep > 1)
                timeProvider.Advance(TimeSpan.FromSeconds(TimerIntervalSeconds));
            allSwept = SpinWait.SpinUntil(() => meter.Sweeps >= sweep, TimeSpan.FromSeconds(10));
        }
        await sweeper.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
        sweeper.Dispose();

        //Assert
        Assert.True(allSwept, $"The sweeper recorded only {meter.Sweeps} of {SweepsToRun} sweeps");
        var earlierMean = MeanBytesPerSweep(meter, WarmUpSweeps);
        var lastMean = MeanBytesPerSweep(meter, SweepsToRun - WindowSize);
        Assert.True(lastMean <= earlierMean,
            $"Mean bytes per sweep grew from {earlierMean} (sweeps {WarmUpSweeps + 1}-{WarmUpSweeps + WindowSize}) to {lastMean} (the last {WindowSize} sweeps)");
    }

    private static double MeanBytesPerSweep(AllocationReadingSweeperMeter meter, int firstSweep)
    {
        long total = 0;
        for (var sweep = firstSweep; sweep < firstSweep + WindowSize; sweep++)
            total += meter.BytesAllocatedBySweep(sweep);
        return (double)total / WindowSize;
    }
}
