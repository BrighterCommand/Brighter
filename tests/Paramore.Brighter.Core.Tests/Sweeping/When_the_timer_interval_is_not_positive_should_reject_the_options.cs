using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class TimedOutboxSweeperOptionsValidationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void When_the_timer_interval_is_not_positive_should_reject_the_options(int timerInterval)
    {
        //Arrange
        var scopeFactory = new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var options = new TimedOutboxSweeperOptions { TimerInterval = timerInterval };

        //Act
        var exception = Record.Exception(() => new TimedOutboxSweeper(scopeFactory, new InMemoryLock(), options));

        //Assert
        Assert.IsType<ConfigurationException>(exception);
    }
}
