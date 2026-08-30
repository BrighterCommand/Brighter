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
using System.Linq;
using System.Transactions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.Observability;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class CompletedPipelineScopeDisposalLoggingTests
{
    [Fact]
    public void When_a_completed_transform_pipeline_scope_disposal_throws_the_post_should_still_succeed()
    {
        //arrange — an FR-22.2-conformant lifetime triple: all three Scoped. PoisonedScopeCompletingMapper
        //resolves IPoisonedDependency successfully (tracked by the pipeline scope for disposal) and
        //carries no failing sibling transform, so the wrap pipeline always completes; disposing it then
        //disposes the scope, which throws from IPoisonedDependency's own Dispose()
        TransformPipelineBuilder.ClearPipelineCache();

        var collection = new ServiceCollection();
        collection.AddScoped<IPoisonedDependency, PoisonedDependency>();
        collection.AddScoped<PoisonedScopeCompletingMapper>();
        collection.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Scoped,
            MapperLifetime = ServiceLifetime.Scoped,
            TransformerLifetime = ServiceLifetime.Scoped
        });
        var rootProvider = collection.BuildServiceProvider();

        var mapperFactory = new ServiceProviderMapperFactory(rootProvider);
        var messageMapperRegistry = new MessageMapperRegistry(mapperFactory, null);
        messageMapperRegistry.Register<PoisonedScopeCompletingCommand, PoisonedScopeCompletingMapper>();

        var routingKey = new RoutingKey("test");
        var internalBus = new InternalBus();
        var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
        {
            { routingKey, new InMemoryMessageProducer(internalBus, new Publication { Topic = routingKey, RequestType = typeof(PoisonedScopeCompletingCommand) }) }
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

        //added to Initializer.Factory directly — the one instance every Brighter static logger in this
        //process is bound to — not to ApplicationLogging.LoggerFactory, which another test may reassign
        var loggerProvider = new CapturingLoggerProvider();
        Initializer.Factory.AddProvider(loggerProvider);

        //act — the first Post completes despite its owned scope's disposal throwing
        commandProcessor.Post(new PoisonedScopeCompletingCommand());

        //assert — exactly one Error naming the request type, and the Post did not throw to get here
        var disposalFailures = loggerProvider.Entries
            .Where(e => e.EventId.Name == "FailedToDisposePipelineScope")
            .ToList();
        var disposalFailure = Assert.Single(disposalFailures);
        Assert.Equal(LogLevel.Error, disposalFailure.Level);
        Assert.Contains(nameof(PoisonedScopeCompletingCommand), disposalFailure.Message);

        //assert — the outer OutboxProducerMediator.ReleasePipeline guard did not also log its own Warning
        //for the same failure: the drain's own catch already swallowed it, so pipeline.Dispose() did not throw
        Assert.DoesNotContain(loggerProvider.Entries, e => e.EventId.Name == "FailedToReleasePipeline");

        //act — a second Post is not affected by the first pipeline's release-once guard
        commandProcessor.Post(new PoisonedScopeCompletingCommand());

        //assert — the failure is not latched: a second, separate Error was logged for the second Post
        var disposalFailuresAfterSecondPost = loggerProvider.Entries
            .Where(e => e.EventId.Name == "FailedToDisposePipelineScope")
            .ToList();
        Assert.Equal(2, disposalFailuresAfterSecondPost.Count);
        Assert.All(disposalFailuresAfterSecondPost, e => Assert.Equal(LogLevel.Error, e.Level));
    }
}
