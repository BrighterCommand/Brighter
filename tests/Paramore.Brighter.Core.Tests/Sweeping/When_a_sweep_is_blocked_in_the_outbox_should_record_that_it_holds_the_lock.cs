using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class SweeperStateBlockedInOutboxTests
{
    [Test]
    public async Task When_a_sweep_is_blocked_in_the_outbox_should_record_that_it_holds_the_lock()
    {
        //Arrange
        var timeProvider = new FakeTimeProvider();
        var state = new OutboxSweeperState(timeProvider);
        using var mediator = new GatedOutboxProducerMediator();
        var services = new ServiceCollection();
        services.AddSingleton<IAmAnOutboxProducerMediator>(mediator);
        using var provider = services.BuildServiceProvider();
        var sweeper = new TimedOutboxSweeper(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new InMemoryLock(),
            new TimedOutboxSweeperOptions { TimerInterval = 60, MinimumMessageAge = TimeSpan.Zero },
            timeProvider: timeProvider,
            state: state);

        OutboxSweeperSnapshot blocked;
        try
        {
            await sweeper.StartAsync(CancellationToken.None);
            var reachedMediator = mediator.WaitForSweepToStart(TimeSpan.FromSeconds(10));

            //Act
            timeProvider.Advance(TimeSpan.FromSeconds(25));
            blocked = state.Read();

            //Assert
            await Assert.That(reachedMediator).IsTrue().Because("The sweep did not reach the mediator");
            await Assert.That(blocked.IsSweepInProgress).IsTrue();
            await Assert.That(blocked.RunningTime).IsEqualTo(TimeSpan.FromSeconds(25));
            await Assert.That(blocked.HoldsLock).IsTrue();
            await Assert.That(blocked.IsActive).IsTrue();
        }
        finally
        {
            var stopping = sweeper.StopAsync(CancellationToken.None);
            mediator.Open();
            await stopping.WaitAsync(TimeSpan.FromSeconds(10));
            sweeper.Dispose();
        }
    }
}
