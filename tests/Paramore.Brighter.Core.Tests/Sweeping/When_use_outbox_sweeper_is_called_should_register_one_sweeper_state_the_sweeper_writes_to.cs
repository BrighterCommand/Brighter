using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class UseOutboxSweeperRegistersSweeperStateTests
{
    [Fact]
    public void When_use_outbox_sweeper_is_called_should_register_one_sweeper_state_resolved_as_the_same_instance()
    {
        //Arrange
        using var lockGate = new GatedDistributedLock();
        var services = SweeperContainer.Create(lockGate, new FakeTimeProvider(), useOutboxSweeper: true);
        using var provider = services.BuildServiceProvider();

        //Act
        var first = provider.GetService<OutboxSweeperState>();
        var second = provider.GetService<OutboxSweeperState>();

        //Assert
        Assert.NotNull(first);
        Assert.Same(first, second);
    }

    [Fact]
    public async Task When_use_outbox_sweeper_is_called_should_start_a_sweeper_that_writes_to_the_registered_state_on_the_container_clock()
    {
        //Arrange
        var timeProvider = new FakeTimeProvider();
        using var lockGate = new GatedDistributedLock();
        var services = SweeperContainer.Create(lockGate, timeProvider, useOutboxSweeper: true);
        using var provider = services.BuildServiceProvider();
        var state = provider.GetRequiredService<OutboxSweeperState>();
        var sweeper = provider.GetServices<IHostedService>().OfType<TimedOutboxSweeper>().Single();

        try
        {
            await sweeper.StartAsync(CancellationToken.None);
            var reachedLock = lockGate.WaitForSweepToStart(TimeSpan.FromSeconds(10));

            //Act
            timeProvider.Advance(TimeSpan.FromSeconds(7));
            var blocked = state.Read();

            //Assert
            Assert.True(reachedLock, "The sweep did not reach the lock");
            Assert.True(blocked.IsSweepInProgress);
            Assert.Equal(TimeSpan.FromSeconds(7), blocked.RunningTime);
        }
        finally
        {
            lockGate.Open();
            await sweeper.StopAsync(new CancellationToken(canceled: true));
        }
    }

    [Fact]
    public void When_use_outbox_sweeper_is_called_twice_should_register_one_sweeper_state_and_one_sweeper()
    {
        //Arrange
        using var lockGate = new GatedDistributedLock();
        var services = SweeperContainer.Create(lockGate, new FakeTimeProvider(), useOutboxSweeper: true);
        SweeperContainer.NewBuilder(services).UseOutboxSweeper();

        //Act
        using var provider = services.BuildServiceProvider();

        //Assert
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(OutboxSweeperState));
        Assert.Single(provider.GetServices<IHostedService>().OfType<TimedOutboxSweeper>());
    }
}
