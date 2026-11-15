using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Outbox.Hosting;
using System.Threading.Tasks;
namespace Paramore.Brighter.Core.Tests.Sweeping;

public class TimedOutboxSweeperOptionsValidationTests
{
    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    public async Task When_the_timer_interval_is_not_positive_should_reject_the_options(int timerInterval)
    {
        //Arrange
        var scopeFactory = new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var options = new TimedOutboxSweeperOptions { TimerInterval = timerInterval };

        //Act
        var exception = TestExceptionRecorder.Capture(() => new TimedOutboxSweeper(scopeFactory, new InMemoryLock(), options));

        //Assert
        await Assert.That(exception).IsTypeOf<ConfigurationException>();
    }
}
