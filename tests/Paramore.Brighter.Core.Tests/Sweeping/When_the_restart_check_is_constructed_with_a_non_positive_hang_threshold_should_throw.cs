using System;
using Paramore.Brighter.Outbox.Hosting;
using Paramore.Brighter.Outbox.Hosting.HealthChecks;
using System.Threading.Tasks;
namespace Paramore.Brighter.Core.Tests.Sweeping;

public class RestartCheckNonPositiveHangThresholdTests
{
    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    public async Task When_the_restart_check_is_constructed_with_a_non_positive_hang_threshold_should_throw(int seconds)
    {
        //Arrange
        var options = new TimedOutboxSweeperOptions { TimerInterval = 10 };

        //Act
        var exception = TestExceptionRecorder.Capture(() => new OutboxSweeperRestartHealthCheck(null, options, TimeSpan.FromSeconds(seconds)));

        //Assert
        var argumentException = await Assert.That(exception).IsTypeOf<ArgumentOutOfRangeException>();
        await Assert.That(argumentException.ParamName).IsEqualTo("hangThreshold");
    }

    [Test]
    public async Task When_the_restart_check_is_constructed_with_a_non_positive_hang_threshold_should_throw_but_not_for_a_positive_threshold_below_the_timer_interval()
    {
        //Arrange
        var options = new TimedOutboxSweeperOptions { TimerInterval = 10 };

        //Act
        var exception = TestExceptionRecorder.Capture(() => new OutboxSweeperRestartHealthCheck(null, options, TimeSpan.FromSeconds(5)));

        //Assert
        await Assert.That(exception).IsNull();
    }
}
