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

// ADR 0075 bracket 2 - a Publish subscriber's own execution, not merely its resolution, must stay
// suppressed, because a nested Send or Post the subscriber's handler issues at dispatch time goes
// through the singleton CommandProcessor from user code, a pipeline core never built and holds no
// reference to. Bracket 1 alone (T5.2) cannot reach it: by the time a subscriber's Handle/HandleAsync
// runs, its own resolution is already over. Once the publish returns, suppression must not have leaked
// onto the caller's own flow - a Send or Post issued next must adopt exactly as it would have before the
// publish (NFR-4).
public class NestedPipelineSuppressionTests
{
    [Fact]
    public void When_a_subscriber_issues_a_nested_pipeline_it_should_not_adopt_the_ambient()
    {
        // Arrange - a JoinAmbient host, all three lifetimes Scoped, with a live ambient established on
        // the caller's own flow through an AsyncLocal-backed provider (the shape a non-ASP.NET console
        // host uses). One NestedPipelinePublishedEvent subscriber, whose own Handle issues a nested
        // Send and a nested Post before returning
        TransformPipelineBuilder.ClearPipelineCache();

        var recorder = new UnitOfWorkRecorder();
        var scopeProvider = new AsyncLocalScopeProvider();
        CommandProcessor commandProcessor = null!;

        var collection = new ServiceCollection();
        collection.AddScoped<IUnitOfWork, UnitOfWork>();
        collection.AddSingleton(recorder);
        collection.AddScoped<NestedPipelineSubscriber>();
        collection.AddScoped<AmbientAdoptionCommandHandler>();
        collection.AddScoped<AmbientAdoptionPostMapper>();
        collection.AddScoped<ScopedArtefactCache>();
        collection.AddSingleton<IAmAScopeProvider>(scopeProvider);
        collection.AddSingleton<IAmACommandProcessor>(_ => commandProcessor);
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Scoped,
            MapperLifetime = ServiceLifetime.Scoped,
            TransformerLifetime = ServiceLifetime.Scoped,
            DefaultScopeAffinity = ScopeAffinity.JoinAmbient
        });
        var rootProvider = collection.BuildServiceProvider();

        var subscriberRegistry = new SubscriberRegistry();
        subscriberRegistry.Register<NestedPipelinePublishedEvent, NestedPipelineSubscriber>();
        subscriberRegistry.Register<AmbientAdoptionCommand, AmbientAdoptionCommandHandler>();

        var handlerFactory = new ServiceProviderHandlerFactory(rootProvider);

        var mapperFactory = new ServiceProviderMapperFactory(rootProvider);
        var mapperRegistry = new MessageMapperRegistry(mapperFactory, null);
        mapperRegistry.Register<AmbientAdoptionPostCommand, AmbientAdoptionPostMapper>();

        var routingKey = new RoutingKey("test");
        var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
        {
            { routingKey, new InMemoryMessageProducer(new InternalBus(), new Publication { Topic = routingKey, RequestType = typeof(AmbientAdoptionPostCommand) }) }
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

        commandProcessor = new CommandProcessor(
            subscriberRegistry,
            handlerFactory,
            new InMemoryRequestContextFactory(),
            new DefaultPolicy(),
            resiliencePipelineRegistry,
            bus,
            new InMemorySchedulerFactory()
        );

        // Act - establish the ambient, capture its own IUnitOfWork, then Publish
        using var ambientScope = rootProvider.CreateScope();
        var ambientUnitOfWork = ambientScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        scopeProvider.Establish(new AsyncLocalAmbientScope(ambientScope.ServiceProvider));

        commandProcessor.Publish(new NestedPipelinePublishedEvent());

        // Assert - the nested Send and nested Post, issued from inside the subscriber's own Handle,
        // both resolved a fresh, already-disposed instance Brighter created - neither adopted the
        // ambient still live on this flow
        AssertNestedResolutionsDidNotAdopt(recorder, ambientUnitOfWork);

        // Act - a Send and a Post issued outside any subscriber, after the publish has already
        // returned, on the same flow the ambient was established on
        commandProcessor.Send(new AmbientAdoptionCommand());
        commandProcessor.Post(new AmbientAdoptionPostCommand());

        scopeProvider.Clear();

        // Assert - the caller's own flow was left unsuppressed once the publish returned: both resolved
        // the ambient's own, still-live instance
        AssertOutsideResolutionsAdopted(recorder, ambientUnitOfWork);
    }

    [Fact]
    public async Task When_an_async_subscriber_issues_a_nested_pipeline_it_should_not_adopt_the_ambient()
    {
        // Arrange - the async twin: PublishAsync starts every subscriber on the caller's flow and awaits
        // them together, rather than dispatching through Parallel.ForEach
        TransformPipelineBuilder.ClearPipelineCache();

        var recorder = new UnitOfWorkRecorder();
        var scopeProvider = new AsyncLocalScopeProvider();
        CommandProcessor commandProcessor = null!;

        var collection = new ServiceCollection();
        collection.AddScoped<IUnitOfWork, UnitOfWork>();
        collection.AddSingleton(recorder);
        collection.AddScoped<NestedPipelineSubscriberAsync>();
        collection.AddScoped<AmbientAdoptionCommandHandler>();
        collection.AddScoped<AmbientAdoptionPostMapper>();
        collection.AddScoped<ScopedArtefactCache>();
        collection.AddSingleton<IAmAScopeProvider>(scopeProvider);
        collection.AddSingleton<IAmACommandProcessor>(_ => commandProcessor);
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Scoped,
            MapperLifetime = ServiceLifetime.Scoped,
            TransformerLifetime = ServiceLifetime.Scoped,
            DefaultScopeAffinity = ScopeAffinity.JoinAmbient
        });
        var rootProvider = collection.BuildServiceProvider();

        var subscriberRegistry = new SubscriberRegistry();
        subscriberRegistry.RegisterAsync<NestedPipelinePublishedEvent, NestedPipelineSubscriberAsync>();
        subscriberRegistry.Register<AmbientAdoptionCommand, AmbientAdoptionCommandHandler>();

        var handlerFactory = new ServiceProviderHandlerFactory(rootProvider);

        var mapperFactory = new ServiceProviderMapperFactory(rootProvider);
        var mapperRegistry = new MessageMapperRegistry(mapperFactory, null);
        mapperRegistry.Register<AmbientAdoptionPostCommand, AmbientAdoptionPostMapper>();

        var routingKey = new RoutingKey("test");
        var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
        {
            { routingKey, new InMemoryMessageProducer(new InternalBus(), new Publication { Topic = routingKey, RequestType = typeof(AmbientAdoptionPostCommand) }) }
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

        commandProcessor = new CommandProcessor(
            subscriberRegistry,
            handlerFactory,
            new InMemoryRequestContextFactory(),
            new DefaultPolicy(),
            resiliencePipelineRegistry,
            bus,
            new InMemorySchedulerFactory()
        );

        // Act - establish the ambient, capture its own IUnitOfWork, then PublishAsync
        using var ambientScope = rootProvider.CreateScope();
        var ambientUnitOfWork = ambientScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        scopeProvider.Establish(new AsyncLocalAmbientScope(ambientScope.ServiceProvider));

        await commandProcessor.PublishAsync(new NestedPipelinePublishedEvent());

        // Assert - the nested Send and nested Post, issued from inside the subscriber's own
        // HandleAsync, both resolved a fresh, already-disposed instance Brighter created
        AssertNestedResolutionsDidNotAdopt(recorder, ambientUnitOfWork);

        // Act - a Send and a Post issued outside any subscriber, after the publish has already
        // returned, on the same flow the ambient was established on
        commandProcessor.Send(new AmbientAdoptionCommand());
        commandProcessor.Post(new AmbientAdoptionPostCommand());

        scopeProvider.Clear();

        // Assert - the caller's own flow was left unsuppressed once the publish returned
        AssertOutsideResolutionsAdopted(recorder, ambientUnitOfWork);
    }

    private static void AssertNestedResolutionsDidNotAdopt(UnitOfWorkRecorder recorder, IUnitOfWork ambientUnitOfWork)
    {
        Assert.Equal(2, recorder.UnitsOfWork.Count);
        var nestedSendUnitOfWork = recorder.UnitsOfWork[0];
        var nestedPostUnitOfWork = recorder.UnitsOfWork[1];
        Assert.NotSame(ambientUnitOfWork, nestedSendUnitOfWork);
        Assert.NotSame(ambientUnitOfWork, nestedPostUnitOfWork);
        Assert.True(nestedSendUnitOfWork.IsDisposed);
        Assert.True(nestedPostUnitOfWork.IsDisposed);
    }

    private static void AssertOutsideResolutionsAdopted(UnitOfWorkRecorder recorder, IUnitOfWork ambientUnitOfWork)
    {
        Assert.Equal(4, recorder.UnitsOfWork.Count);
        var outsideSendUnitOfWork = recorder.UnitsOfWork[2];
        var outsidePostUnitOfWork = recorder.UnitsOfWork[3];
        Assert.Same(ambientUnitOfWork, outsideSendUnitOfWork);
        Assert.Same(ambientUnitOfWork, outsidePostUnitOfWork);
        Assert.False(ambientUnitOfWork.IsDisposed);
    }
}
