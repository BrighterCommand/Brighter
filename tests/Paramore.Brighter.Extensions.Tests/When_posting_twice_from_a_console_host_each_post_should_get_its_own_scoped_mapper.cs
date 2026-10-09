using System.Collections.Generic;
using System.Transactions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.Observability;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class ScopedMapperPerPostTests
{
    [Fact]
    public void When_posting_twice_from_a_console_host_each_post_should_get_its_own_scoped_mapper()
    {
        //arrange — a console host: no ambient, no IAmAScopeProvider registered. An FR-22.2-conformant
        //lifetime triple: all three Scoped
        TransformPipelineBuilder.ClearPipelineCache();

        var recorder = new ConstructionOrderRecorder();
        var collection = new ServiceCollection();
        collection.AddSingleton(recorder);
        collection.AddScoped<PostedMapper>();
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Scoped,
            MapperLifetime = ServiceLifetime.Scoped,
            TransformerLifetime = ServiceLifetime.Scoped
        });
        var rootProvider = collection.BuildServiceProvider();

        var mapperFactory = new ServiceProviderMapperFactory(rootProvider);
        var messageMapperRegistry = new MessageMapperRegistry(mapperFactory, null);
        messageMapperRegistry.Register<PostedCommand, PostedMapper>();

        var routingKey = new RoutingKey("test");
        var internalBus = new InternalBus();
        var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
        {
            { routingKey, new InMemoryMessageProducer(internalBus, new Publication { Topic = routingKey, RequestType = typeof(PostedCommand) }) }
        });

        var timeProvider = new FakeTimeProvider();
        var tracer = new BrighterTracer(timeProvider);
        var resiliencePipelineRegistry = new ResiliencePipelineRegistry<string>().AddBrighterDefault();

        IAmAnOutboxProducerMediator bus = new OutboxProducerMediator<Message, CommittableTransaction>(
            producerRegistry,
            resiliencePipelineRegistry,
            messageMapperRegistry,
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            tracer,
            new FindPublicationByPublicationTopicOrRequestType(),
            new InMemoryOutbox(timeProvider) { Tracer = tracer }
        );

        var commandProcessor = new CommandProcessor(
            new InMemoryRequestContextFactory(),
            new DefaultPolicy(),
            resiliencePipelineRegistry,
            bus,
            new InMemorySchedulerFactory()
        );

        //act — Post(commandA) completes, then Post(commandB) is called
        commandProcessor.Post(new PostedCommand());
        commandProcessor.Post(new PostedCommand());

        //assert — two distinct mapper instances, the first disposed strictly before the second was
        //constructed (the ordering, not merely the distinctness)
        Assert.Equal(new[] { "Constructed:1", "Disposed:1", "Constructed:2", "Disposed:2" }, recorder.Events);
    }
}
