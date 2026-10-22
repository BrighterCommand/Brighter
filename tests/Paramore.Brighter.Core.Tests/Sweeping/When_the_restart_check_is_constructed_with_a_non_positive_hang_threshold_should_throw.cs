using System;
using Paramore.Brighter.Outbox.Hosting;
using Paramore.Brighter.Outbox.Hosting.HealthChecks;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class RestartCheckNonPositiveHangThresholdTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void When_the_restart_check_is_constructed_with_a_non_positive_hang_threshold_should_throw(int seconds)
    {
        //Arrange
        var options = new TimedOutboxSweeperOptions { TimerInterval = 10 };

        //Act
        var exception = Record.Exception(() => new OutboxSweeperRestartHealthCheck(null, options, TimeSpan.FromSeconds(seconds)));

        //Assert
        var argumentException = Assert.IsType<ArgumentOutOfRangeException>(exception);
        Assert.Equal("hangThreshold", argumentException.ParamName);
    }

    [Fact]
    public void When_the_restart_check_is_constructed_with_a_non_positive_hang_threshold_should_throw_but_not_for_a_positive_threshold_below_the_timer_interval()
    {
        //Arrange
        var options = new TimedOutboxSweeperOptions { TimerInterval = 10 };

        //Act
        var exception = Record.Exception(() => new OutboxSweeperRestartHealthCheck(null, options, TimeSpan.FromSeconds(5)));

        //Assert
        Assert.Null(exception);
    }
}
