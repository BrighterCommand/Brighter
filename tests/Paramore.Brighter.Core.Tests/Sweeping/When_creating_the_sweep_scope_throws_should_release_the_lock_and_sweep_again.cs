using System;
using System.Collections.Generic;
using System.Linq;
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

public class TimedOutboxSweeperScopeFailureTests
{
    private static readonly TimeSpan s_waitForSweep = TimeSpan.FromSeconds(3);

    [Fact]
    public async Task When_creating_the_sweep_scope_throws_should_release_the_lock_and_sweep_again()
    {
        //Arrange
        var routingKey = new RoutingKey("MyCommand");
        var bus = new InternalBus();
        var outbox = new InMemoryOutbox(TimeProvider.System);

        var undispatched = new Message(
            new MessageHeader(Id.Random(), routingKey, MessageType.MT_COMMAND),
            new MessageBody("awaiting a publish confirmation that never came"));
        outbox.Add(undispatched, new RequestContext());

        var services = new ServiceCollection().AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory>(Initializer.TestLoggerFactory);
        services.AddSingleton<IAmAnOutboxProducerMediator>(CreateMediator(bus, routingKey, outbox));
        await using var provider = services.BuildServiceProvider();

        var sweeper = new TimedOutboxSweeper(
serviceScopeFactory: new FailsFirstScopeFactory(provider.GetRequiredService<IServiceScopeFactory>()), distributedLock: new InMemoryLock(), options: new TimedOutboxSweeperOptions { TimerInterval = 1, MinimumMessageAge = TimeSpan.Zero }, logger: Microsoft.Extensions.Logging.LoggerFactoryExtensions.CreateLogger<TimedOutboxSweeper>(Initializer.TestLoggerFactory));

        //Act
        await sweeper.StartAsync(CancellationToken.None);
        var swept = SpinWait.SpinUntil(() => bus.Stream(routingKey).Any(), s_waitForSweep);
        await sweeper.StopAsync(CancellationToken.None);
        sweeper.Dispose();

        //Assert
        Assert.True(swept, $"The sweeper did not sweep again within {s_waitForSweep} after failing to create its scope");
    }

    private static OutboxProducerMediator<Message, CommittableTransaction> CreateMediator(
        InternalBus bus, RoutingKey routingKey, InMemoryOutbox outbox)
    {
        var producer = new InMemoryMessageProducer(bus, publication: new Publication { Topic = routingKey }, loggerFactory: Initializer.TestLoggerFactory);
        var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
        {
            { routingKey, producer }
        });

        return new OutboxProducerMediator<Message, CommittableTransaction>(
producerRegistry: producerRegistry, resiliencePipelineRegistry: new ResiliencePipelineRegistry<string>().AddBrighterDefault(), mapperRegistry: new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => null!), null), messageTransformerFactory: new EmptyMessageTransformerFactory(), messageTransformerFactoryAsync: new EmptyMessageTransformerFactoryAsync(), tracer: new BrighterTracer(TimeProvider.System), publicationFinder: new FindPublicationByPublicationTopicOrRequestType(), outbox: outbox, loggerFactory: Initializer.TestLoggerFactory);
    }
}
