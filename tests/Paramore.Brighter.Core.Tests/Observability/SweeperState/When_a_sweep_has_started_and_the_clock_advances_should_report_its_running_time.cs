using System;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.SweeperState;

public class SweeperStateRunningTimeTests
{
    [Fact]
    public void When_a_sweep_has_started_and_the_clock_advances_should_report_its_running_time()
    {
        //Arrange
        var clock = new FakeTimeProvider();
        var state = new OutboxSweeperState(clock);
        state.SweepStarted();

        //Act
        clock.Advance(TimeSpan.FromSeconds(25));
        var snapshot = state.Read();

        //Assert
        Assert.True(snapshot.IsSweepInProgress);
        Assert.Equal(TimeSpan.FromSeconds(25), snapshot.RunningTime);
    }

    [Fact]
    public void When_a_sweep_ends_should_report_no_running_time_even_after_the_clock_advances()
    {
        //Arrange
        var clock = new FakeTimeProvider();
        var state = new OutboxSweeperState(clock);
        state.SweepStarted();
        clock.Advance(TimeSpan.FromSeconds(25));

        //Act
        state.SweepEnded(SweepOutcome.Completed);
        clock.Advance(TimeSpan.FromSeconds(60));
        var snapshot = state.Read();

        //Assert
        Assert.False(snapshot.IsSweepInProgress);
        Assert.Equal(TimeSpan.Zero, snapshot.RunningTime);
    }
}
