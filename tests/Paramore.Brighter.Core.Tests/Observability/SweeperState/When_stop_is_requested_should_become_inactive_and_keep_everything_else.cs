using System;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.SweeperState;

public class SweeperStateStopTests
{
    [Fact]
    public void When_stop_is_requested_after_failed_sweeps_should_become_inactive_and_keep_everything_else()
    {
        //Arrange
        var state = new OutboxSweeperState(new FakeTimeProvider());
        for (var i = 0; i < 3; i++)
        {
            state.SweepStarted();
            state.SweepEnded(SweepOutcome.Failed);
        }

        //Act
        state.Stopped();
        var snapshot = state.Read();

        //Assert
        Assert.Equal(3, snapshot.ConsecutiveFailedSweeps);
        Assert.Equal(SweepOutcome.Failed, snapshot.LastOutcome);
        Assert.True(snapshot.IsStopped);
        Assert.False(snapshot.IsActive);
    }

    [Fact]
    public void When_stop_is_requested_after_a_sweep_that_obtained_the_lock_should_not_be_active()
    {
        //Arrange
        var state = new OutboxSweeperState(new FakeTimeProvider());
        state.SweepStarted();
        state.LockObtained();
        state.SweepEnded(SweepOutcome.Completed);
        Assert.True(state.Read().IsActive);

        //Act
        state.Stopped();
        var snapshot = state.Read();

        //Assert
        Assert.True(snapshot.IsStopped);
        Assert.False(snapshot.IsActive);
    }

    [Fact]
    public void When_stop_is_requested_during_a_sweep_should_keep_timing_the_sweep()
    {
        //Arrange
        var clock = new FakeTimeProvider();
        var state = new OutboxSweeperState(clock);
        state.SweepStarted();
        clock.Advance(TimeSpan.FromSeconds(25));

        //Act
        state.Stopped();
        clock.Advance(TimeSpan.FromSeconds(31));
        var snapshot = state.Read();

        //Assert
        Assert.True(snapshot.IsSweepInProgress);
        Assert.Equal(TimeSpan.FromSeconds(56), snapshot.RunningTime);
    }

    [Fact]
    public void When_a_sweep_ends_after_a_stop_should_record_the_outcome_and_stay_inactive()
    {
        //Arrange
        var state = new OutboxSweeperState(new FakeTimeProvider());
        state.SweepStarted();
        state.LockObtained();
        state.Stopped();

        //Act
        state.SweepEnded(SweepOutcome.Failed);
        var snapshot = state.Read();

        //Assert
        Assert.Equal(SweepOutcome.Failed, snapshot.LastOutcome);
        Assert.Equal(1, snapshot.ConsecutiveFailedSweeps);
        Assert.True(snapshot.IsStopped);
        Assert.False(snapshot.IsActive);
    }

    [Fact]
    public void When_stop_is_requested_twice_should_be_idempotent()
    {
        //Arrange
        var state = new OutboxSweeperState(new FakeTimeProvider());
        state.SweepStarted();
        state.LockObtained();
        state.SweepEnded(SweepOutcome.Failed);
        state.Stopped();
        var afterFirstStop = state.Read();

        //Act
        state.Stopped();
        var afterSecondStop = state.Read();

        //Assert
        Assert.Equal(afterFirstStop, afterSecondStop);
        Assert.True(afterSecondStop.IsStopped);
    }
}
