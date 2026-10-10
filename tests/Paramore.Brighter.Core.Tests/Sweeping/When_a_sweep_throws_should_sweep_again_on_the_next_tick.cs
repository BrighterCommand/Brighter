using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class TimedOutboxSweeperFailedSweepTests
{
    private static readonly TimeSpan s_waitForSweep = TimeSpan.FromSeconds(3);

    [Fact]
    public async Task When_a_sweep_throws_should_sweep_again_on_the_next_tick()
    {
        //Arrange
        var routingKey = new RoutingKey("MyCommand");
        var bus = new InternalBus();
        var outbox = new InMemoryOutbox(TimeProvider.System);
        var mediator = CreateMediator(bus, routingKey, outbox);

        var undispatched = new Message(
            new MessageHeader(Id.Random(), routingKey, MessageType.MT_COMMAND),
            new MessageBody("awaiting a publish confirmation that never came"));
        outbox.Add(undispatched, new RequestContext());

        var sweepsAttempted = 0;
        var services = new ServiceCollection();
        services.AddTransient<IAmAnOutboxProducerMediator>(_ =>
            Interlocked.Increment(ref sweepsAttempted) == 1
                ? throw new InvalidOperationException("the first sweep fails")
                : mediator);
        await using var provider = services.BuildServiceProvider();

        var sweeper = new TimedOutboxSweeper(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new InMemoryLock(),
            new TimedOutboxSweeperOptions { TimerInterval = 1, MinimumMessageAge = TimeSpan.Zero });

        //Act
        await sweeper.StartAsync(CancellationToken.None);
        var swept = SpinWait.SpinUntil(() => bus.Stream(routingKey).Any(), s_waitForSweep);
        await sweeper.StopAsync(CancellationToken.None);
        sweeper.Dispose();

        //Assert
        Assert.True(swept, $"The sweeper did not recover from a failed sweep within {s_waitForSweep}");
    }

    private static OutboxProducerMediator<Message, CommittableTransaction> CreateMediator(
        InternalBus bus, RoutingKey routingKey, InMemoryOutbox outbox)
    {
        var producer = new InMemoryMessageProducer(bus, new Publication { Topic = routingKey });
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
            outbox);
    }
}
