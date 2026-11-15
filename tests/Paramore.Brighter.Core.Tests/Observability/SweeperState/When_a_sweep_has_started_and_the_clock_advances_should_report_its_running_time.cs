using System;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Observability;
using System.Threading.Tasks;
namespace Paramore.Brighter.Core.Tests.Observability.SweeperState;

public class SweeperStateRunningTimeTests
{
    [Test]
    public async Task When_a_sweep_has_started_and_the_clock_advances_should_report_its_running_time()
    {
        //Arrange
        var clock = new FakeTimeProvider();
        var state = new OutboxSweeperState(clock);
        state.SweepStarted();

        //Act
        clock.Advance(TimeSpan.FromSeconds(25));
        var snapshot = state.Read();

        //Assert
        await Assert.That(snapshot.IsSweepInProgress).IsTrue();
        await Assert.That(snapshot.RunningTime).IsEqualTo(TimeSpan.FromSeconds(25));
    }

    [Test]
    public async Task When_a_sweep_ends_should_report_no_running_time_even_after_the_clock_advances()
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
        await Assert.That(snapshot.IsSweepInProgress).IsFalse();
        await Assert.That(snapshot.RunningTime).IsEqualTo(TimeSpan.Zero);
    }
}
