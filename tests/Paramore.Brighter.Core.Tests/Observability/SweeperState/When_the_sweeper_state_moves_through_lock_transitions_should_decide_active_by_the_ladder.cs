using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.SweeperState;

public class SweeperStateActiveLadderTests
{
    [Fact]
    public void When_the_sweeper_state_moves_through_lock_transitions_should_decide_active_by_the_ladder()
    {
        //Arrange
        var state = new OutboxSweeperState(new FakeTimeProvider());

        //Act
        state.SweepStarted();
        state.LockObtained();
        var snapshot = state.Read();

        //Assert
        Assert.True(snapshot.IsActive);
        Assert.True(snapshot.HoldsLock);
    }

    [Fact]
    public void When_a_sweep_is_still_acquiring_the_lock_after_a_completed_sweep_should_not_be_active()
    {
        //Arrange
        var state = new OutboxSweeperState(new FakeTimeProvider());
        state.SweepStarted();
        state.LockObtained();
        state.SweepEnded(SweepOutcome.Completed);

        //Act
        state.SweepStarted();
        var snapshot = state.Read();

        //Assert
        Assert.False(snapshot.IsActive);
        Assert.False(snapshot.HoldsLock);
    }

    [Fact]
    public void When_the_last_sweep_obtained_the_lock_and_completed_should_be_active()
    {
        //Arrange
        var state = new OutboxSweeperState(new FakeTimeProvider());
        state.SweepStarted();
        state.LockObtained();

        //Act
        state.SweepEnded(SweepOutcome.Completed);
        var snapshot = state.Read();

        //Assert
        Assert.True(snapshot.IsActive);
        Assert.False(snapshot.HoldsLock);
    }

    [Fact]
    public void When_the_last_sweep_obtained_the_lock_and_failed_should_be_active()
    {
        //Arrange
        var state = new OutboxSweeperState(new FakeTimeProvider());
        state.SweepStarted();
        state.LockObtained();

        //Act
        state.SweepEnded(SweepOutcome.Failed);
        var snapshot = state.Read();

        //Assert
        Assert.True(snapshot.IsActive);
    }

    [Fact]
    public void When_a_completed_sweep_is_followed_by_lock_unavailable_should_not_be_active()
    {
        //Arrange
        var state = new OutboxSweeperState(new FakeTimeProvider());
        state.SweepStarted();
        state.LockObtained();
        state.SweepEnded(SweepOutcome.Completed);

        //Act
        state.SweepStarted();
        state.SweepEnded(SweepOutcome.LockUnavailable);
        var snapshot = state.Read();

        //Assert
        Assert.False(snapshot.IsActive);
    }

    [Fact]
    public void When_lock_unavailable_is_followed_by_a_sweep_that_obtained_the_lock_and_failed_should_be_active()
    {
        //Arrange
        var state = new OutboxSweeperState(new FakeTimeProvider());
        state.SweepStarted();
        state.SweepEnded(SweepOutcome.LockUnavailable);

        //Act
        state.SweepStarted();
        state.LockObtained();
        state.SweepEnded(SweepOutcome.Failed);
        var snapshot = state.Read();

        //Assert
        Assert.True(snapshot.IsActive);
    }

    [Fact]
    public void When_a_sweep_fails_before_obtaining_the_lock_should_not_be_active()
    {
        //Arrange
        var state = new OutboxSweeperState(new FakeTimeProvider());
        state.SweepStarted();

        //Act
        state.SweepEnded(SweepOutcome.Failed);
        var snapshot = state.Read();

        //Assert
        Assert.False(snapshot.IsActive);
    }

    [Fact]
    public void When_the_lock_is_obtained_with_no_sweep_in_progress_should_ignore_it()
    {
        //Arrange
        var state = new OutboxSweeperState(new FakeTimeProvider());

        //Act
        state.LockObtained();
        var snapshot = state.Read();

        //Assert
        Assert.False(snapshot.HoldsLock);
        Assert.False(snapshot.IsActive);
    }
}
