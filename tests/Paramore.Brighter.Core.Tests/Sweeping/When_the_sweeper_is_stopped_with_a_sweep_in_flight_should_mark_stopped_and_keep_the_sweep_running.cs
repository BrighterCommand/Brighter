using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class SweeperStateStoppedTests
{
    [Test]
    public async Task When_the_sweeper_is_stopped_with_a_sweep_in_flight_should_mark_stopped_and_keep_the_sweep_running()
    {
        //Arrange
        var timeProvider = new FakeTimeProvider();
        var state = new OutboxSweeperState(timeProvider);
        using var mediator = new GatedOutboxProducerMediator();
        var services = new ServiceCollection();
        services.AddSingleton<IAmAnOutboxProducerMediator>(mediator);
        using var provider = services.BuildServiceProvider();
        var sweeper = CreateSweeper(provider, timeProvider, state);

        try
        {
            await sweeper.StartAsync(CancellationToken.None);
            var reachedMediator = mediator.WaitForSweepToStart(TimeSpan.FromSeconds(10));
            timeProvider.Advance(TimeSpan.FromSeconds(25));

            //Act
            await sweeper.StopAsync(new CancellationToken(canceled: true));
            timeProvider.Advance(TimeSpan.FromSeconds(31));
            var abandoned = state.Read();

            //Assert
            await Assert.That(reachedMediator).IsTrue().Because("The sweep did not reach the mediator");
            await Assert.That(abandoned.IsStopped).IsTrue();
            await Assert.That(abandoned.IsActive).IsFalse();
            await Assert.That(abandoned.IsSweepInProgress).IsTrue();
            await Assert.That(abandoned.RunningTime).IsEqualTo(TimeSpan.FromSeconds(56));
        }
        finally
        {
            mediator.Open();
            sweeper.Dispose();
        }
    }

    [Test]
    public async Task When_the_sweeper_is_stopped_before_it_started_should_mark_stopped()
    {
        //Arrange
        var timeProvider = new FakeTimeProvider();
        var state = new OutboxSweeperState(timeProvider);
        using var mediator = new GatedOutboxProducerMediator();
        var services = new ServiceCollection();
        services.AddSingleton<IAmAnOutboxProducerMediator>(mediator);
        using var provider = services.BuildServiceProvider();
        var sweeper = CreateSweeper(provider, timeProvider, state);

        //Act
        await sweeper.StopAsync(CancellationToken.None);
        var stopped = state.Read();
        sweeper.Dispose();

        //Assert
        await Assert.That(stopped.IsStopped).IsTrue();
        await Assert.That(stopped.IsActive).IsFalse();
    }

    [Test]
    public async Task When_the_sweeper_is_disposed_should_mark_stopped()
    {
        //Arrange
        var timeProvider = new FakeTimeProvider();
        var state = new OutboxSweeperState(timeProvider);
        using var mediator = new GatedOutboxProducerMediator();
        var services = new ServiceCollection();
        services.AddSingleton<IAmAnOutboxProducerMediator>(mediator);
        using var provider = services.BuildServiceProvider();
        var sweeper = CreateSweeper(provider, timeProvider, state);

        //Act
        sweeper.Dispose();
        var disposed = state.Read();

        //Assert
        await Assert.That(disposed.IsStopped).IsTrue();
        await Assert.That(disposed.IsActive).IsFalse();
    }

    private static TimedOutboxSweeper CreateSweeper(ServiceProvider provider, FakeTimeProvider timeProvider, OutboxSweeperState state) =>
        new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new InMemoryLock(),
            new TimedOutboxSweeperOptions { TimerInterval = 60, MinimumMessageAge = TimeSpan.Zero },
            timeProvider: timeProvider,
            state: state);
}
