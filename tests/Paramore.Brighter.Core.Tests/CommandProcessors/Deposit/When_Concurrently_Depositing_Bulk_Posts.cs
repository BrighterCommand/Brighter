using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Observability;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Deposit;

public class ConcurrentBulkDepositPostTests
{
    [Fact]
    public async Task When_concurrently_depositing_bulk_posts_async_no_messages_are_lost_or_errored()
    {
        const int workers = 16;
        const int requestsPerWorker = 8;
        const int expectedMessageCount = workers * requestsPerWorker;

        var topic = new RoutingKey("MyCommand");
        var bus = new InternalBus();
        var outbox = new InMemoryOutbox(new FakeTimeProvider()) { Tracer = new BrighterTracer() };

        var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
        {
            { topic, new InMemoryMessageProducer(bus, new Publication { Topic = topic, RequestType = typeof(MyCommand) }) }
        });

        var mapperRegistry = new MessageMapperRegistry(
            null,
            new SimpleMessageMapperFactoryAsync(type =>
            {
                if (type == typeof(MyCommandMessageMapperAsync)) return new MyCommandMessageMapperAsync();
                throw new ConfigurationException($"No mapper registered for {type.Name}");
            })
        );
        mapperRegistry.RegisterAsync<MyCommand, MyCommandMessageMapperAsync>();

        var resiliencePipelineRegistry = new ResiliencePipelineRegistry<string>().AddBrighterDefault();
        IAmAnOutboxProducerMediator mediator = new OutboxProducerMediator<Message, CommittableTransaction>(
            producerRegistry,
            resiliencePipelineRegistry,
            mapperRegistry,
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            new BrighterTracer(),
            new FindPublicationByPublicationTopicOrRequestType(),
            outbox);

        var commandProcessor = new CommandProcessor(
            new InMemoryRequestContextFactory(),
            new DefaultPolicy(),
            resiliencePipelineRegistry,
            mediator,
            new InMemorySchedulerFactory());

        var errors = new ConcurrentQueue<Exception>();
        var depositedIds = new ConcurrentBag<Id[]>();
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var tasks = Enumerable.Range(0, workers)
            .Select(_ => Task.Run(async () =>
            {
                await gate.Task;

                try
                {
                    var requests = Enumerable.Range(1, requestsPerWorker)
                        .Select(IRequest (i) => new MyCommand { Value = $"command-{i}" })
                        .ToList();

                    var ids = await commandProcessor.DepositPostAsync(requests);
                    depositedIds.Add(ids);
                }
                catch (Exception ex)
                {
                    errors.Enqueue(ex);
                }
            }))
            .ToArray();

        gate.TrySetResult(true);
        await Task.WhenAll(tasks);

        Assert.Empty(errors);

        var allIds = depositedIds.SelectMany(ids => ids).ToArray();
        Assert.Equal(expectedMessageCount, allIds.Length);
        Assert.Equal(expectedMessageCount, allIds.Distinct().Count());

        await commandProcessor.ClearOutboxAsync(allIds);

        var flushedMessages = bus.Stream(topic).ToArray();
        Assert.Equal(expectedMessageCount, flushedMessages.Length);
        Assert.Equal(expectedMessageCount, flushedMessages.Select(m => m.Id).Distinct().Count());
    }
}


