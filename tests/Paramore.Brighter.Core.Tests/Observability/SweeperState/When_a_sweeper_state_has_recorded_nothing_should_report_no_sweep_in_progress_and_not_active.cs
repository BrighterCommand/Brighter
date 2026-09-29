using System;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Observability;
using System.Threading.Tasks;
namespace Paramore.Brighter.Core.Tests.Observability.SweeperState;

public class SweeperStateNothingRecordedTests
{
    [Test]
    public async Task When_a_sweeper_state_has_recorded_nothing_should_report_no_sweep_in_progress_and_not_active()
    {
        //Arrange
        var state = new OutboxSweeperState(new FakeTimeProvider());

        //Act
        var snapshot = state.Read();

        //Assert
        await Assert.That(snapshot.IsSweepInProgress).IsFalse();
        await Assert.That(snapshot.RunningTime).IsEqualTo(TimeSpan.Zero);
        await Assert.That(snapshot.HoldsLock).IsFalse();
        await Assert.That(snapshot.ConsecutiveFailedSweeps).IsEqualTo(0);
        await Assert.That(snapshot.LastOutcome).IsNull();
        await Assert.That(snapshot.IsStopped).IsFalse();
        await Assert.That(snapshot.IsActive).IsFalse();
    }

    [Test]
    public async System.Threading.Tasks.Task When_a_sweeper_state_is_created_without_a_clock_should_throw()
    {
        //Arrange //Act //Assert
        await Assert.That(() => new OutboxSweeperState(null!)).ThrowsExactly<ArgumentNullException>();
    }
}
