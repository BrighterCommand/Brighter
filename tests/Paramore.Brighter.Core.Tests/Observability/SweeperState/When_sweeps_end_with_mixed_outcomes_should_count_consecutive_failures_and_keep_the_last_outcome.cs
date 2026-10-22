using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.SweeperState;

public class SweeperStateConsecutiveFailureTests
{
    [Fact]
    public void When_sweeps_end_with_mixed_outcomes_should_count_consecutive_failures_and_keep_the_last_outcome()
    {
        //Arrange
        var state = new OutboxSweeperState(new FakeTimeProvider());

        //Act
        EndSweeps(state, SweepOutcome.Failed, SweepOutcome.Failed, SweepOutcome.LockUnavailable, SweepOutcome.Failed);
        var snapshot = state.Read();

        //Assert
        Assert.Equal(3, snapshot.ConsecutiveFailedSweeps);
        Assert.Equal(SweepOutcome.Failed, snapshot.LastOutcome);
    }

    [Fact]
    public void When_a_sweep_completes_between_failures_should_restart_the_count()
    {
        //Arrange
        var state = new OutboxSweeperState(new FakeTimeProvider());

        //Act
        EndSweeps(state, SweepOutcome.Failed, SweepOutcome.Failed, SweepOutcome.Completed, SweepOutcome.Failed);
        var snapshot = state.Read();

        //Assert
        Assert.Equal(1, snapshot.ConsecutiveFailedSweeps);
        Assert.Equal(SweepOutcome.Failed, snapshot.LastOutcome);
    }

    [Fact]
    public void When_a_sweep_completes_after_failures_should_reset_the_count_to_zero()
    {
        //Arrange
        var state = new OutboxSweeperState(new FakeTimeProvider());

        //Act
        EndSweeps(state, SweepOutcome.Failed, SweepOutcome.Failed, SweepOutcome.Completed);
        var snapshot = state.Read();

        //Assert
        Assert.Equal(0, snapshot.ConsecutiveFailedSweeps);
        Assert.Equal(SweepOutcome.Completed, snapshot.LastOutcome);
    }

    [Fact]
    public void When_a_sweep_cannot_get_the_lock_after_failures_should_leave_the_count_unchanged()
    {
        //Arrange
        var state = new OutboxSweeperState(new FakeTimeProvider());

        //Act
        EndSweeps(state, SweepOutcome.Failed, SweepOutcome.Failed, SweepOutcome.LockUnavailable);
        var snapshot = state.Read();

        //Assert
        Assert.Equal(2, snapshot.ConsecutiveFailedSweeps);
        Assert.Equal(SweepOutcome.LockUnavailable, snapshot.LastOutcome);
    }

    private static void EndSweeps(OutboxSweeperState state, params SweepOutcome[] outcomes)
    {
        foreach (var outcome in outcomes)
        {
            state.SweepStarted();
            state.SweepEnded(outcome);
        }
    }
}
