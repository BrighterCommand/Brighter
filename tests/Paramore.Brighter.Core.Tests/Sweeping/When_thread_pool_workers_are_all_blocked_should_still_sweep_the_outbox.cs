using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Transactions;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Sweeping;

[Collection(ThreadPoolStarvationCollection.Name)]
public class TimedOutboxSweeperStarvationTests
{
    private static readonly TimeSpan s_waitForSweep = TimeSpan.FromSeconds(3);

    [Fact]
    public void When_thread_pool_workers_are_all_blocked_should_still_sweep_the_outbox()
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

        var services = new ServiceCollection().AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory>(Initializer.TestLoggerFactory);
        services.AddSingleton<IAmAnOutboxProducerMediator>(mediator);
        using var provider = services.BuildServiceProvider();

        var sweeper = new TimedOutboxSweeper(
serviceScopeFactory: provider.GetRequiredService<IServiceScopeFactory>(), distributedLock: new InMemoryLock(), options: new TimedOutboxSweeperOptions { TimerInterval = 1, MinimumMessageAge = TimeSpan.Zero }, logger: Microsoft.Extensions.Logging.LoggerFactoryExtensions.CreateLogger<TimedOutboxSweeper>(Initializer.TestLoggerFactory));

        var starvedPool = new StarvedThreadPool();
        try
        {
            //Act
            sweeper.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
            var swept = SpinWait.SpinUntil(() => bus.Stream(routingKey).Any(), s_waitForSweep);

            //Assert
            Assert.True(swept, $"The sweeper did not dispatch the outstanding message within {s_waitForSweep} while every thread-pool worker was blocked");
        }
        finally
        {
            starvedPool.Dispose();
            sweeper.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
            sweeper.Dispose();
        }
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
