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
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.Observability;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

// AC-30 (FR-24.1), ADR 0072 steps 1b, 2 — all three lifetimes Scoped, so both the Send's handler
// pipeline and the Post's transform pipeline consult the ambient source (FR-27.1). Both verbs are
// exercised against the same host and the same throwing provider: the two verbs build different
// pipelines whose builders differ in what they clean up before the fault leaves, so the Post branch
// is not redundant with the Send branch.
public class AmbientQueryThrowsUnwrappedTests
{
    [Fact]
    public void When_the_ambient_query_throws_the_caller_should_see_it_unwrapped()
    {
        // Arrange — a ScopeTracker shared by both factories so "no pipeline scope is leaked" is
        // falsifiable, not just "the call did not throw"
        TransformPipelineBuilder.ClearPipelineCache();

        var scopeTracker = BuildScopeTracker(out var trackingProvider);

        var subscriberRegistry = new SubscriberRegistry();
        subscriberRegistry.Register<AmbientThrowsCommand, AmbientThrowsCommandHandler>();
        var handlerFactory = new ServiceProviderHandlerFactory(trackingProvider);

        var mapperFactory = new ServiceProviderMapperFactory(trackingProvider);
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

        // one CommandProcessor - the same host - handles both Send (via subscriberRegistry/handlerFactory)
        // and Post (via bus)
        var commandProcessor = new CommandProcessor(
            subscriberRegistry,
            handlerFactory,
            new InMemoryRequestContextFactory(),
            new DefaultPolicy(),
            resiliencePipelineRegistry,
            bus,
            new InMemorySchedulerFactory()
        );

        // Act & Assert — Send: the caller observes the ambient source's own exception unwrapped, not a
        // ConfigurationException, and no pipeline scope was created
        var sendException = Assert.Throws<InvalidOperationException>(() =>
            commandProcessor.Send(new AmbientThrowsCommand()));
        Assert.Equal("the ambient source's own fault", sendException.Message);
        Assert.Equal(0, scopeTracker.CreatedCount);

        // Act & Assert — Post, in the same host: the same exception, unwrapped, and still no pipeline
        // scope leaked
        var postException = Assert.Throws<InvalidOperationException>(() =>
            commandProcessor.Post(new AmbientThrowsPostCommand()));
        Assert.Equal("the ambient source's own fault", postException.Message);
        Assert.Equal(0, scopeTracker.CreatedCount);
    }

    private static ScopeTracker BuildScopeTracker(out IServiceProvider trackingProvider)
    {
        var collection = new ServiceCollection();
        collection.AddScoped<AmbientThrowsCommandHandler>();
        collection.AddScoped<AmbientThrowsPostMapper>();
        collection.AddSingleton<IAmAScopeProvider>(new ThrowingScopeProvider());
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Scoped,
            MapperLifetime = ServiceLifetime.Scoped,
            TransformerLifetime = ServiceLifetime.Scoped
        });
        var rootProvider = collection.BuildServiceProvider();

        var scopeTracker = new ScopeTracker(rootProvider.GetRequiredService<IServiceScopeFactory>());
        trackingProvider = new TrackingServiceProvider(rootProvider, scopeTracker);
        return scopeTracker;
    }
}
