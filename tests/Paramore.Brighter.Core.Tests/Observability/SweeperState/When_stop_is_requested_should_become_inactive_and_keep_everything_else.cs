using System;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Observability;
using System.Threading.Tasks;
namespace Paramore.Brighter.Core.Tests.Observability.SweeperState;

public class SweeperStateStopTests
{
    [Test]
    public async Task When_stop_is_requested_after_failed_sweeps_should_become_inactive_and_keep_everything_else()
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
        await Assert.That(snapshot.ConsecutiveFailedSweeps).IsEqualTo(3);
        await Assert.That(snapshot.LastOutcome).IsEqualTo(SweepOutcome.Failed);
        await Assert.That(snapshot.IsStopped).IsTrue();
        await Assert.That(snapshot.IsActive).IsFalse();
    }

    [Test]
    public async Task When_stop_is_requested_after_a_sweep_that_obtained_the_lock_should_not_be_active()
    {
        //Arrange
        var state = new OutboxSweeperState(new FakeTimeProvider());
        state.SweepStarted();
        state.LockObtained();
        state.SweepEnded(SweepOutcome.Completed);
        await Assert.That(state.Read().IsActive).IsTrue();

        //Act
        state.Stopped();
        var snapshot = state.Read();

        //Assert
        await Assert.That(snapshot.IsStopped).IsTrue();
        await Assert.That(snapshot.IsActive).IsFalse();
    }

    [Test]
    public async Task When_stop_is_requested_during_a_sweep_should_keep_timing_the_sweep()
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
        await Assert.That(snapshot.IsSweepInProgress).IsTrue();
        await Assert.That(snapshot.RunningTime).IsEqualTo(TimeSpan.FromSeconds(56));
    }

    [Test]
    public async Task When_a_sweep_ends_after_a_stop_should_record_the_outcome_and_stay_inactive()
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
        await Assert.That(snapshot.LastOutcome).IsEqualTo(SweepOutcome.Failed);
        await Assert.That(snapshot.ConsecutiveFailedSweeps).IsEqualTo(1);
        await Assert.That(snapshot.IsStopped).IsTrue();
        await Assert.That(snapshot.IsActive).IsFalse();
    }

    [Test]
    public async Task When_stop_is_requested_twice_should_be_idempotent()
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
        await Assert.That(afterSecondStop).IsEqualTo(afterFirstStop);
        await Assert.That(afterSecondStop.IsStopped).IsTrue();
    }
}
