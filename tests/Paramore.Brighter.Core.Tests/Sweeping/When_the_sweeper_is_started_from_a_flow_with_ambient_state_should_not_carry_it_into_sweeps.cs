using System;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class TimedOutboxSweeperAmbientStateTests
{
    private static readonly AsyncLocal<string?> s_ambient = new();

    [Fact]
    public async Task When_the_sweeper_is_started_from_a_flow_with_ambient_state_should_not_carry_it_into_sweeps()
    {
        //Arrange
        using var outbox = new SweepableOutbox();
        var distributedLock = new AmbientRecordingLock(s_ambient);
        var sweeper = new TimedOutboxSweeper(
            outbox.ScopeFactory,
            distributedLock,
            new TimedOutboxSweeperOptions { TimerInterval = 1, MinimumMessageAge = TimeSpan.Zero });

        //Act
        await Task.Run(async () =>
        {
            s_ambient.Value = "the starting caller's state"; // e.g. a logging scope or baggage
            await sweeper.StartAsync(CancellationToken.None);
        });
        var swept = distributedLock.WaitForSweep(TimeSpan.FromSeconds(3));
        await sweeper.StopAsync(CancellationToken.None);
        sweeper.Dispose();

        //Assert
        Assert.True(swept, "The sweeper did not sweep");
        Assert.Null(distributedLock.AmbientWhenObtained);
    }
}
