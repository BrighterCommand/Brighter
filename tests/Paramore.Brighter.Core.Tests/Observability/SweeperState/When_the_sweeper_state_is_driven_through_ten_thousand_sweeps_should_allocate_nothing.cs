using System;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.SweeperState;

public class SweeperStateAllocationTests
{
    private const int Rounds = 10_000;

    [Fact]
    public void When_the_sweeper_state_is_driven_through_ten_thousand_sweeps_should_allocate_nothing()
    {
        //Arrange
        var clock = new FakeTimeProvider();
        var state = new OutboxSweeperState(clock);
        Drive(state, Rounds);

        //Act
        var before = GC.GetAllocatedBytesForCurrentThread();
        Drive(state, Rounds);
        var after = GC.GetAllocatedBytesForCurrentThread();

        //Assert
        Assert.Equal(0, after - before);
    }

    private static void Drive(OutboxSweeperState state, int rounds)
    {
        for (var round = 0; round < rounds; round++)
        {
            state.SweepStarted();
            state.LockObtained();
            state.SweepEnded((round % 3) switch
            {
                0 => SweepOutcome.Completed,
                1 => SweepOutcome.Failed,
                _ => SweepOutcome.LockUnavailable
            });
            state.Read();
        }
    }
}
