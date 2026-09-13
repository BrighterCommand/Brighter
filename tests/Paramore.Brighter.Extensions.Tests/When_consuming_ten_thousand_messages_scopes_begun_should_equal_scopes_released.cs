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
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

// AC-23 (NFR-5, NFR-6), ADR 0070 step 9a, ADR 0071 step 5 — all three lifetimes Scoped. Placed after
// T2.3 deliberately: only from there do both the mapper/transform family and the handler family reach
// their DI scope through the same CreatePipelineScope()/ServiceProviderPipelineScope mechanism, so one
// ScopeTracker (shared by both factories) counts both families with a single, stable instrument.
public class ScopeGrowthOverSustainedConsumptionTests
{
    private const int MessageCount = 10_000;

    [Fact]
    public void When_consuming_ten_thousand_messages_scopes_begun_should_equal_scopes_released()
    {
        // Arrange — one ScopeTracker shared by the mapper factory (wrap pipeline, per message) and the
        // handler factory (handler pipeline, per Send); each pipeline resolves two Scoped artefacts —
        // itself and its own ScopeCountingDependency — from what should be one shared DI scope, so a
        // regression to "one scope per resolution" would double the observed count rather than leave it
        // unchanged (the positive control AC-37's own Note calls for)
        TransformPipelineBuilder.ClearPipelineCache();

        var scopeTracker = BuildScopeTracker(out var trackingProvider);

        var mapperFactory = new ServiceProviderMapperFactory(trackingProvider);
        var mapperRegistry = new MessageMapperRegistry(mapperFactory, null);
        mapperRegistry.Register<ScopeCountingCommand, ScopeCountingMapper>();
        var pipelineBuilder = new TransformPipelineBuilder(mapperRegistry, new EmptyMessageTransformerFactory());

        var subscriberRegistry = new SubscriberRegistry();
        subscriberRegistry.Register<ScopeCountingCommand, ScopeCountingCommandHandler>();
        var handlerFactory = new ServiceProviderHandlerFactory(trackingProvider);
        var commandProcessor = new CommandProcessor(
            subscriberRegistry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(),
            new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());

        // Act — consume 10,000 messages: one wrap pipeline (mapper family) and one Send (handler
        // family) per message, each built and released before the next begins
        for (var i = 0; i < MessageCount; i++)
        {
            var wrapPipeline = pipelineBuilder.BuildWrapPipeline<ScopeCountingCommand>();
            wrapPipeline.Dispose();

            commandProcessor.Send(new ScopeCountingCommand());
        }

        // Assert — one scope begun and one released per pipeline (two pipelines per message), zero live
        Assert.Equal(2 * MessageCount, scopeTracker.CreatedCount);
        Assert.Equal(scopeTracker.CreatedCount, scopeTracker.DisposedCount);
        Assert.Equal(0, scopeTracker.OutstandingCount);
    }

    private static ScopeTracker BuildScopeTracker(out IServiceProvider trackingProvider)
    {
        var collection = new ServiceCollection();
        collection.AddScoped<ScopeCountingDependency>();
        collection.AddScoped<ScopeCountingMapper>();
        collection.AddScoped<ScopeCountingCommandHandler>();
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
