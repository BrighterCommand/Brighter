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

using System;
using System.Collections.Generic;
using System.Transactions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.Observability;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

// design-owed (FR-23, FR-12; ADR 0072 steps 2d, 4) - adoption creates a genuine race window that no
// acceptance criterion reaches: an ambient can pass the usability probe and be adopted, and then have
// its owner dispose the underlying scope before a later resolution runs through it. That is a fault,
// not a declined adoption, so it must not surface as a raw framework ObjectDisposedException - it must
// be translated into a ConfigurationException naming the ambient's own provider, at the one borrowed
// resolution path every container-backed factory shares.
public class BorrowedAmbientDisposedMidPipelineTests
{
    [Fact]
    public void When_a_borrowed_ambient_is_disposed_mid_pipeline_a_send_should_surface_a_configuration_error()
    {
        // Arrange - a JoinAmbient host, all three lifetimes Scoped, whose ambient's own scope will be
        // disposed by its owner immediately after this pipeline's own usability probe passes
        var recorder = new HandlerMarkerRecorder();
        var scopeProvider = new AsyncLocalScopeProvider();

        var collection = new ServiceCollection();
        collection.AddScoped<IMarker, Marker>();
        collection.AddScoped<ScopedHandlerCommandHandler>();
        collection.AddSingleton(recorder);
        collection.AddSingleton<IAmAScopeProvider>(scopeProvider);
        collection.TryAddScoped<ScopedArtefactCache>();
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Scoped,
            MapperLifetime = ServiceLifetime.Scoped,
            TransformerLifetime = ServiceLifetime.Scoped,
            DefaultScopeAffinity = ScopeAffinity.JoinAmbient
        });
        var rootProvider = collection.BuildServiceProvider();

        var subscriberRegistry = new SubscriberRegistry();
        subscriberRegistry.Register<ScopedHandlerCommand, ScopedHandlerCommandHandler>();
        var handlerFactory = new ServiceProviderHandlerFactory(rootProvider);
        var commandProcessor = new CommandProcessor(
            subscriberRegistry,
            handlerFactory,
            new InMemoryRequestContextFactory(),
            new DefaultPolicy(),
            new ResiliencePipelineRegistry<string>().AddBrighterDefault(),
            new InMemorySchedulerFactory()
        );

        // Arrange - establish an ambient whose own scope is still alive right now, but which disposes
        // itself the moment the probe's own ScopedArtefactCache resolution passes - before the handler
        // is actually resolved through it
        var ambientScope = rootProvider.CreateScope();
        scopeProvider.Establish(new AsyncLocalAmbientScope(new DisposeAfterProbeServiceProvider(ambientScope)));

        // Act
        var exception = Assert.Throws<ConfigurationException>(() =>
            commandProcessor.Send(new ScopedHandlerCommand()));

        scopeProvider.Clear();

        // Assert - thrown directly (PipelineBuilder's own catch filter excludes ConfigurationException),
        // naming the ambient's own provider implementation type, carrying the disposal fault as its
        // inner exception
        Assert.Contains(nameof(AsyncLocalScopeProvider), exception.Message);
        Assert.Contains("disposed while a pipeline was resolving from it", exception.Message);
        Assert.IsType<ObjectDisposedException>(exception.InnerException);

        // Assert - nothing was latched: this is a fault, not a declined adoption, so a later Send
        // against a fresh, healthy ambient adopts it normally rather than being forced onto its own scope
        using var freshAmbientScope = rootProvider.CreateScope();
        var freshMarker = freshAmbientScope.ServiceProvider.GetRequiredService<IMarker>();
        scopeProvider.Establish(new AsyncLocalAmbientScope(freshAmbientScope.ServiceProvider));

        commandProcessor.Send(new ScopedHandlerCommand());

        scopeProvider.Clear();

        var resolvedFromFreshAmbient = Assert.Single(recorder.Markers);
        Assert.Same(freshMarker, resolvedFromFreshAmbient);
    }

    [Fact]
    public void When_a_borrowed_ambient_is_disposed_mid_pipeline_a_post_should_carry_it_as_the_inner_exception()
    {
        // Arrange - the same race, on the transform pipeline a Post builds
        var scopeProvider = new AsyncLocalScopeProvider();

        var collection = new ServiceCollection();
        collection.AddScoped<IMarker, Marker>();
        collection.AddScoped<MarkerMapper>();
        collection.AddSingleton(new MarkerLog());
        collection.AddSingleton<IAmAScopeProvider>(scopeProvider);
        collection.TryAddScoped<ScopedArtefactCache>();
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Scoped,
            MapperLifetime = ServiceLifetime.Scoped,
            TransformerLifetime = ServiceLifetime.Scoped,
            DefaultScopeAffinity = ScopeAffinity.JoinAmbient
        });
        var rootProvider = collection.BuildServiceProvider();

        var mapperFactory = new ServiceProviderMapperFactory(rootProvider);
        var mapperRegistry = new MessageMapperRegistry(mapperFactory, null);
        mapperRegistry.Register<MarkerCommand, MarkerMapper>();

        var routingKey = new RoutingKey("test");
        var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
        {
            { routingKey, new InMemoryMessageProducer(new InternalBus(), new Publication { Topic = routingKey, RequestType = typeof(MarkerCommand) }) }
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

        var ambientScope = rootProvider.CreateScope();
        scopeProvider.Establish(new AsyncLocalAmbientScope(new DisposeAfterProbeServiceProvider(ambientScope)));

        // Act
        var exception = Assert.Throws<ConfigurationException>(() =>
            commandProcessor.Post(new MarkerCommand()));

        scopeProvider.Clear();

        // Assert - the transform builder's own catches carry no filter, so the translated exception
        // arrives wrapped, as the inner exception, rather than thrown directly as with Send
        var inner = Assert.IsType<ConfigurationException>(exception.InnerException);
        Assert.Contains(nameof(AsyncLocalScopeProvider), inner.Message);
        Assert.Contains("disposed while a pipeline was resolving from it", inner.Message);
        Assert.IsType<ObjectDisposedException>(inner.InnerException);
    }
}
