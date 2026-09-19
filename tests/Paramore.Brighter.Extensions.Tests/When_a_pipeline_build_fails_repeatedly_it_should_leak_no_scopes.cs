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

public class PipelineBuildFailureScopeLeakTests
{
    [Fact]
    public void When_a_pipeline_build_fails_repeatedly_it_should_leak_no_scopes()
    {
        //arrange — an FR-22.2-conformant lifetime triple: all three Scoped. UnresolvableMapper's
        //constructor depends on IUnregisteredDependency, which is never registered, so every pipeline
        //build fails
        TransformPipelineBuilder.ClearPipelineCache();

        var collection = new ServiceCollection();
        collection.AddScoped<UnresolvableMapper>();
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Scoped,
            MapperLifetime = ServiceLifetime.Scoped,
            TransformerLifetime = ServiceLifetime.Scoped
        });
        var rootProvider = collection.BuildServiceProvider();

        var scopeTracker = new ScopeTracker(rootProvider.GetRequiredService<IServiceScopeFactory>());
        var trackingProvider = new TrackingServiceProvider(rootProvider, scopeTracker);

        var mapperFactory = new ServiceProviderMapperFactory(trackingProvider);
        var messageMapperRegistry = new MessageMapperRegistry(mapperFactory, null);
        messageMapperRegistry.Register<UnresolvableCommand, UnresolvableMapper>();

        var routingKey = new RoutingKey("test");
        var internalBus = new InternalBus();
        var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
        {
            { routingKey, new InMemoryMessageProducer(internalBus, new Publication { Topic = routingKey, RequestType = typeof(UnresolvableCommand) }) }
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

        //act — 1,000 Post attempts, each failing to build the pipeline
        for (var i = 0; i < 1_000; i++)
        {
            var configurationException = Assert.Throws<ConfigurationException>(() => commandProcessor.Post(new UnresolvableCommand()));

            //assert — the caller still sees the original resolution failure, not just that building failed
            Assert.IsType<System.InvalidOperationException>(configurationException.InnerException);
        }

        //assert — every Brighter-created pipeline scope was released; none left live (NFR-5)
        Assert.Equal(1_000, scopeTracker.CreatedCount);
        Assert.Equal(0, scopeTracker.OutstandingCount);
    }
}
