#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System.Collections.Generic;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.Observability;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

// Only a pipeline with a Scoped participant should ever ask whether there is an ambient DI scope to
// join. An all-Transient pipeline should ask zero times, even though a provider is registered and the
// host opted in - the lifetimes rule it out before the provider is ever consulted. A pipeline that mixes
// a Scoped participant with a Transient one still asks (the Scoped participant is the one offering a
// scope at all), but it always asks for a brand new scope, never to join an ambient one, because the
// Transient participant can't share whatever the ambient scope's lifetime turns out to be. Neither host
// runs pipeline validation first, so this also shows the fallback holds with no validation pass at all.
public class AmbientQueryParticipationTests
{
    [Fact]
    public async Task When_no_participating_factory_is_scoped_the_ambient_source_should_not_be_asked()
    {
        // Arrange — every lifetime is Transient (BrighterOptions' own default), so no factory involved
        // is ever the Scoped one that would ask. The handler pipeline still gets its own scope handle for
        // its per-resolution isolation, but that must not be treated as an ask either. The host opts in
        // to joining an ambient scope anyway, so the zero-asks result comes from the lifetimes involved,
        // not from the host never wanting to join one in the first place.
        TransformPipelineBuilder.ClearPipelineCache();

        var recordingProvider = new RecordingScopeProvider();
        var unitOfWorkRecorder = new UnitOfWorkRecorder();

        var collection = new ServiceCollection();
        collection.AddTransient<AmbientThrowsCommandHandler>();
        collection.AddTransient<OrderPlacedHandlerOne>();
        collection.AddTransient<OrderPlacedHandlerTwo>();
        collection.AddTransient<OrderPlacedHandlerThree>();
        collection.AddTransient<IUnitOfWork, UnitOfWork>();
        collection.AddSingleton(unitOfWorkRecorder);
        collection.AddTransient<AmbientThrowsPostMapper>();
        collection.AddSingleton<IAmAScopeProvider>(recordingProvider);
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions { DefaultScopeAffinity = ScopeAffinity.JoinAmbient });
        var rootProvider = collection.BuildServiceProvider();

        var subscriberRegistry = new SubscriberRegistry();
        subscriberRegistry.Register<AmbientThrowsCommand, AmbientThrowsCommandHandler>();
        subscriberRegistry.RegisterAsync<OrderPlaced, OrderPlacedHandlerOne>();
        subscriberRegistry.RegisterAsync<OrderPlaced, OrderPlacedHandlerTwo>();
        subscriberRegistry.RegisterAsync<OrderPlaced, OrderPlacedHandlerThree>();

        var handlerFactory = new ServiceProviderHandlerFactory(rootProvider);

        var mapperFactory = new ServiceProviderMapperFactory(rootProvider);
        var mapperRegistry = new MessageMapperRegistry(mapperFactory, null);
        mapperRegistry.Register<AmbientThrowsPostCommand, AmbientThrowsPostMapper>();

        var routingKey = new RoutingKey("test");
        var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
        {
            { routingKey, new InMemoryMessageProducer(new InternalBus(), new Publication { Topic = routingKey, RequestType = typeof(AmbientThrowsPostCommand) }) }
        });

        var timeProvider = new FakeTimeProvider();
        var tracer = new BrighterTracer(timeProvider);
        var resiliencePipelineRegistry = new ResiliencePipelineRegistry<string>().AddBrighterDefault();

        IAmAnOutboxProducerMediator bus = new OutboxProducerMediator<Message, CommittableTransaction>(
            producerRegistry,
            resiliencePipelineRegistry,
            mapperRegistry,
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            tracer,
            new FindPublicationByPublicationTopicOrRequestType(),
            new InMemoryOutbox(timeProvider) { Tracer = tracer }
        );

        var commandProcessor = new CommandProcessor(
            subscriberRegistry,
            handlerFactory,
            new InMemoryRequestContextFactory(),
            new DefaultPolicy(),
            resiliencePipelineRegistry,
            bus,
            new InMemorySchedulerFactory()
        );

        // Act — Send, Publish (three subscribers) and Post, all against the same all-Transient host
        commandProcessor.Send(new AmbientThrowsCommand());
        await commandProcessor.PublishAsync(new OrderPlaced());
        commandProcessor.Post(new AmbientThrowsPostCommand());

        // Assert — the recording provider was never asked, across all three verbs. This is the whole
        // assertion: it must not be checked by looking at whether the handler pipeline holds a scope
        // handle of its own, because a Transient handler pipeline legitimately holds one for its own
        // per-resolution isolation, and that is unrelated to whether it asked for an ambient scope.
        Assert.Empty(recordingProvider.Asks);
    }

    [Fact]
    public void When_a_pipeline_mixes_scoped_and_transient_participants_the_ask_still_carries_AlwaysNew()
    {
        // Arrange — the mapper is Scoped but the transformer is Transient, a deliberate mix of lifetimes
        // within the one transform pipeline. Even though the host itself asked to join whatever ambient
        // scope is available, having a Transient participant in the mix means the pipeline as a whole
        // must still create its own scope rather than join one. The provider is configured to genuinely
        // offer a resolvable ambient scope, so proving the mapper resolved a different instance than that
        // ambient scope holds is a real assertion, not a vacuous one (there being nothing to differ from).
        TransformPipelineBuilder.ClearPipelineCache();

        var recorder = new UnitOfWorkRecorder();
        var collection = new ServiceCollection();
        collection.AddScoped<IUnitOfWork, UnitOfWork>();
        collection.AddSingleton(recorder);
        collection.AddScoped<MixedLifetimePostMapper>();
        var recordingProvider = new RecordingScopeProvider(new PlaceholderAmbientScope());
        collection.AddSingleton<IAmAScopeProvider>(recordingProvider);
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Transient,
            MapperLifetime = ServiceLifetime.Scoped,
            TransformerLifetime = ServiceLifetime.Transient,
            DefaultScopeAffinity = ScopeAffinity.JoinAmbient
        });
        var rootProvider = collection.BuildServiceProvider();

        // an independently-resolved IUnitOfWork, standing in for what the ambient would have supplied
        // had this pipeline adopted it - nothing yet adopts it (that lands in T4.4), so it must differ
        // from whatever the pipeline itself resolves
        using var ambientScope = rootProvider.CreateScope();
        var ambientUnitOfWork = ambientScope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var mapperFactory = new ServiceProviderMapperFactory(rootProvider);
        var mapperRegistry = new MessageMapperRegistry(mapperFactory, null);
        mapperRegistry.Register<MixedLifetimePostCommand, MixedLifetimePostMapper>();

        var routingKey = new RoutingKey("test");
        var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
        {
            { routingKey, new InMemoryMessageProducer(new InternalBus(), new Publication { Topic = routingKey, RequestType = typeof(MixedLifetimePostCommand) }) }
        });

        var timeProvider = new FakeTimeProvider();
        var tracer = new BrighterTracer(timeProvider);
        var resiliencePipelineRegistry = new ResiliencePipelineRegistry<string>().AddBrighterDefault();

        IAmAnOutboxProducerMediator bus = new OutboxProducerMediator<Message, CommittableTransaction>(
            producerRegistry,
            resiliencePipelineRegistry,
            mapperRegistry,
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

        // Act
        commandProcessor.Post(new MixedLifetimePostCommand());

        // Assert — exactly one ask is made, and it still asks for a brand new scope rather than to join
        // an ambient one, even though the host itself asked to join whatever's available: the mapper is
        // the only Scoped participant, so it's the one that asks, but the Transient transformer alongside
        // it rules out joining an ambient scope for the pipeline as a whole.
        Assert.Equal(new[] { ScopeAffinity.AlwaysNew }, recordingProvider.Asks);

        // Assert — the ambient scope on offer was ignored: the mapper resolved its own IUnitOfWork,
        // not the one living in the ambient scope.
        var resolved = Assert.Single(recorder.UnitsOfWork);
        Assert.NotSame(ambientUnitOfWork, resolved);
    }
}
