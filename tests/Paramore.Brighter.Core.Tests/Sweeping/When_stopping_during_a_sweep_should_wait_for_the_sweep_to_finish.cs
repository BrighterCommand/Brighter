using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class TimedOutboxSweeperStopTests
{
    private static readonly TimeSpan s_graceForStop = TimeSpan.FromMilliseconds(500);

    [Fact]
    public async Task When_stopping_during_a_sweep_should_wait_for_the_sweep_to_finish()
    {
        //Arrange
        var routingKey = new RoutingKey("MyCommand");
        var services = new ServiceCollection();
        services.AddSingleton<IAmAnOutboxProducerMediator>(CreateMediator(routingKey));
        await using var provider = services.BuildServiceProvider();

        using var sweepInFlight = new GatedDistributedLock();
        var sweeper = new TimedOutboxSweeper(
            provider.GetRequiredService<IServiceScopeFactory>(),
            sweepInFlight,
            new TimedOutboxSweeperOptions { TimerInterval = 1, MinimumMessageAge = TimeSpan.Zero });

        await sweeper.StartAsync(CancellationToken.None);
        Assert.True(sweepInFlight.WaitForSweepToStart(TimeSpan.FromSeconds(5)), "No sweep started");

        //Act
        var stopping = Task.Run(() => sweeper.StopAsync(CancellationToken.None));
        var stoppedMidSweep = await Task.WhenAny(stopping, Task.Delay(s_graceForStop)) == stopping;

        sweepInFlight.Open();
        await stopping.WaitAsync(TimeSpan.FromSeconds(5));
        sweeper.Dispose();

        //Assert
        Assert.False(stoppedMidSweep, "StopAsync returned while a sweep was still running");
    }

    private static OutboxProducerMediator<Message, CommittableTransaction> CreateMediator(RoutingKey routingKey)
    {
        var producer = new InMemoryMessageProducer(new InternalBus(), new Publication { Topic = routingKey });
        var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
        {
            { routingKey, producer }
        });

        return new OutboxProducerMediator<Message, CommittableTransaction>(
            producerRegistry,
            new ResiliencePipelineRegistry<string>().AddBrighterDefault(),
            new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => null!), null),
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            new BrighterTracer(TimeProvider.System),
            new FindPublicationByPublicationTopicOrRequestType(),
            new InMemoryOutbox(TimeProvider.System));
    }
}
