using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class SweeperThrowingMeterTests
{
    [Fact]
    public async Task When_the_sweeper_meter_throws_should_still_sweep_the_outbox()
    {
        //Arrange
        using var outbox = new SweepableOutbox();
        var undispatched = outbox.AddUndispatched();
        var sweeper = new TimedOutboxSweeper(
            outbox.ScopeFactory,
            new InMemoryLock(),
            new TimedOutboxSweeperOptions { TimerInterval = 1, MinimumMessageAge = TimeSpan.Zero },
            meter: new ThrowingSweeperMeter());

        //Act
        await sweeper.StartAsync(CancellationToken.None);
        var swept = SpinWait.SpinUntil(
            () => outbox.Bus.Stream(outbox.RoutingKey).Any(m => m.Id == undispatched.Id), TimeSpan.FromSeconds(3));
        await sweeper.StopAsync(CancellationToken.None);
        sweeper.Dispose();

        //Assert
        Assert.True(swept, "A throwing meter stopped the sweeper from dispatching the outstanding message");
    }
}
