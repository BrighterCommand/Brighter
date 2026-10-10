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
public class TimedOutboxSweeperAsyncSendStarvationTests
{
    private const int OutstandingMessages = 5;
    private static readonly TimeSpan s_waitForSweep = TimeSpan.FromSeconds(3);

    [Fact]
    public void When_thread_pool_workers_are_all_blocked_and_sends_complete_off_the_pool_should_still_sweep_a_batch()
    {
        //Arrange
        var routingKey = new RoutingKey("MyCommand");
        var bus = new InternalBus();
        var outbox = new InMemoryOutbox(TimeProvider.System);
        var producer = new OffPoolCompletingProducer(bus, new Publication { Topic = routingKey });
        var mediator = CreateMediator(producer, routingKey, outbox);

        // Several messages, so the sweep must resume after each awaited send to reach the next one
        for (var i = 0; i < OutstandingMessages; i++)
        {
            outbox.Add(new Message(
                new MessageHeader(Id.Random(), routingKey, MessageType.MT_COMMAND),
                new MessageBody("awaiting a publish confirmation that never came")), new RequestContext());
        }

        var services = new ServiceCollection();
        services.AddSingleton<IAmAnOutboxProducerMediator>(mediator);
        using var provider = services.BuildServiceProvider();

        var sweeper = new TimedOutboxSweeper(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new InMemoryLock(),
            new TimedOutboxSweeperOptions { TimerInterval = 1, MinimumMessageAge = TimeSpan.Zero });

        var starvedPool = new StarvedThreadPool();
        try
        {
            //Act
            sweeper.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
            var swept = SpinWait.SpinUntil(() => bus.Stream(routingKey).Count() == OutstandingMessages, s_waitForSweep);

            //Assert
            Assert.True(swept, $"The sweeper dispatched {bus.Stream(routingKey).Count()} of {OutstandingMessages} outstanding messages within {s_waitForSweep} while every thread-pool worker was blocked");
        }
        finally
        {
            starvedPool.Dispose();
            sweeper.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
            sweeper.Dispose();
            producer.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    private static OutboxProducerMediator<Message, CommittableTransaction> CreateMediator(
        IAmAMessageProducer producer, RoutingKey routingKey, InMemoryOutbox outbox)
    {
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
