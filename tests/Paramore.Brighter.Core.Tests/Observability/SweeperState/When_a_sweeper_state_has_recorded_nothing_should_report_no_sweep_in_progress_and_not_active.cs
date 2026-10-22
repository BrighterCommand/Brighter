using System;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.SweeperState;

public class SweeperStateNothingRecordedTests
{
    [Fact]
    public void When_a_sweeper_state_has_recorded_nothing_should_report_no_sweep_in_progress_and_not_active()
    {
        //Arrange
        var state = new OutboxSweeperState(new FakeTimeProvider());

        //Act
        var snapshot = state.Read();

        //Assert
        Assert.False(snapshot.IsSweepInProgress);
        Assert.Equal(TimeSpan.Zero, snapshot.RunningTime);
        Assert.False(snapshot.HoldsLock);
        Assert.Equal(0, snapshot.ConsecutiveFailedSweeps);
        Assert.Null(snapshot.LastOutcome);
        Assert.False(snapshot.IsStopped);
        Assert.False(snapshot.IsActive);
    }

    [Fact]
    public void When_a_sweeper_state_is_created_without_a_clock_should_throw()
    {
        //Arrange //Act //Assert
        Assert.Throws<ArgumentNullException>(() => new OutboxSweeperState(null!));
    }
}
