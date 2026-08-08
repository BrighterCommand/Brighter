using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Inbox.Attributes;
using Paramore.Brighter.Inbox.Handlers;
using Xunit;
using InboxSyncA = Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline.InboxTypeKeyed.SyncA;
using InboxSyncB = Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline.InboxTypeKeyed.SyncB;
using InboxAsyncA = Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline.InboxTypeKeyed.AsyncA;
using InboxAsyncB = Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline.InboxTypeKeyed.AsyncB;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline
{
    // Regression guard for AC-7 / FR-6 (issue #4192): the global-inbox UseInbox attribute is built
    // per runtime type *after* the decorator cache is read, so it never flows through the cache.
    // Even with two same-simple-name handlers (the collision this fix targets), each built pipeline
    // gets its own UseInbox, and the cached memento value for each handler Type excludes UseInbox.
    // Not RED-able: this holds pre-fix too, so it is a preservation guard, not a /test-first cycle.
    public class When_Building_A_Pipeline_With_Global_Inbox_Each_Handler_Gets_Its_Own_UseInbox
    {
        [Fact]
        public void When_two_sync_handlers_share_a_simple_name_each_built_pipeline_gets_its_own_useinbox_and_the_cache_excludes_it_order_AB()
        {
            // Arrange
            PipelineBuilder<MyCommand>.ClearPipelineCache();
            (PipelineBuilder<MyCommand> builderA, PipelineBuilder<MyCommand> builderB) = CreateSyncInboxBuilders();

            // Act — A first warms the cache, then B
            string traceA = TracePipeline(builderA.Build(new MyCommand(), new RequestContext()).First()).ToString();
            string traceB = TracePipeline(builderB.Build(new MyCommand(), new RequestContext()).First()).ToString();

            // Assert — each built pipeline carries its own UseInbox...
            Assert.Contains("UseInboxHandler", traceA);
            Assert.Contains("UseInboxHandler", traceB);

            // ...while neither cached memento value carries UseInbox (it is pushed onto the local
            // attribute list after the cache TryAdd, never into the cached sequence).
            AssertCacheExcludesUseInbox(typeof(InboxSyncA.CollidingInboxHandler));
            AssertCacheExcludesUseInbox(typeof(InboxSyncB.CollidingInboxHandler));
        }

        [Fact]
        public void When_two_sync_handlers_share_a_simple_name_each_built_pipeline_gets_its_own_useinbox_and_the_cache_excludes_it_order_BA()
        {
            // Arrange
            PipelineBuilder<MyCommand>.ClearPipelineCache();
            (PipelineBuilder<MyCommand> builderA, PipelineBuilder<MyCommand> builderB) = CreateSyncInboxBuilders();

            // Act — opposite order: B first, then A
            string traceB = TracePipeline(builderB.Build(new MyCommand(), new RequestContext()).First()).ToString();
            string traceA = TracePipeline(builderA.Build(new MyCommand(), new RequestContext()).First()).ToString();

            // Assert
            Assert.Contains("UseInboxHandler", traceB);
            Assert.Contains("UseInboxHandler", traceA);
            AssertCacheExcludesUseInbox(typeof(InboxSyncA.CollidingInboxHandler));
            AssertCacheExcludesUseInbox(typeof(InboxSyncB.CollidingInboxHandler));
        }

        [Fact]
        public void When_two_async_handlers_share_a_simple_name_each_built_pipeline_gets_its_own_useinbox_and_the_cache_excludes_it_order_AB()
        {
            // Arrange
            PipelineBuilder<MyCommand>.ClearPipelineCache();
            (PipelineBuilder<MyCommand> builderA, PipelineBuilder<MyCommand> builderB) = CreateAsyncInboxBuilders();

            // Act — async A first, then async B
            string traceA = TracePipeline(builderA.BuildAsync(new MyCommand(), new RequestContext(), false).First()).ToString();
            string traceB = TracePipeline(builderB.BuildAsync(new MyCommand(), new RequestContext(), false).First()).ToString();

            // Assert
            Assert.Contains("UseInboxHandlerAsync", traceA);
            Assert.Contains("UseInboxHandlerAsync", traceB);
            AssertCacheExcludesUseInbox(typeof(InboxAsyncA.CollidingInboxHandler));
            AssertCacheExcludesUseInbox(typeof(InboxAsyncB.CollidingInboxHandler));
        }

        [Fact]
        public void When_two_async_handlers_share_a_simple_name_each_built_pipeline_gets_its_own_useinbox_and_the_cache_excludes_it_order_BA()
        {
            // Arrange
            PipelineBuilder<MyCommand>.ClearPipelineCache();
            (PipelineBuilder<MyCommand> builderA, PipelineBuilder<MyCommand> builderB) = CreateAsyncInboxBuilders();

            // Act — opposite order: async B first, then async A
            string traceB = TracePipeline(builderB.BuildAsync(new MyCommand(), new RequestContext(), false).First()).ToString();
            string traceA = TracePipeline(builderA.BuildAsync(new MyCommand(), new RequestContext(), false).First()).ToString();

            // Assert
            Assert.Contains("UseInboxHandlerAsync", traceB);
            Assert.Contains("UseInboxHandlerAsync", traceA);
            AssertCacheExcludesUseInbox(typeof(InboxAsyncA.CollidingInboxHandler));
            AssertCacheExcludesUseInbox(typeof(InboxAsyncB.CollidingInboxHandler));
        }

        private static (PipelineBuilder<MyCommand>, PipelineBuilder<MyCommand>) CreateSyncInboxBuilders()
        {
            IAmAnInboxSync inbox = new InMemoryInbox(new FakeTimeProvider());

            var registryA = new SubscriberRegistry();
            registryA.Register<MyCommand, InboxSyncA.CollidingInboxHandler>();

            var registryB = new SubscriberRegistry();
            registryB.Register<MyCommand, InboxSyncB.CollidingInboxHandler>();

            var container = new ServiceCollection();
            container.AddTransient<InboxSyncA.CollidingInboxHandler>();
            container.AddTransient<InboxSyncB.CollidingInboxHandler>();
            container.AddTransient<MyValidationHandler<MyCommand>>();
            container.AddSingleton(inbox);
            container.AddTransient<UseInboxHandler<MyCommand>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });

            var factory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());
            var inboxConfiguration = new InboxConfiguration();

            return (
                new PipelineBuilder<MyCommand>(registryA, (IAmAHandlerFactorySync)factory, inboxConfiguration),
                new PipelineBuilder<MyCommand>(registryB, (IAmAHandlerFactorySync)factory, inboxConfiguration));
        }

        private static (PipelineBuilder<MyCommand>, PipelineBuilder<MyCommand>) CreateAsyncInboxBuilders()
        {
            IAmAnInboxAsync inbox = new InMemoryInbox(new FakeTimeProvider());

            var registryA = new SubscriberRegistry();
            registryA.RegisterAsync<MyCommand, InboxAsyncA.CollidingInboxHandler>();

            var registryB = new SubscriberRegistry();
            registryB.RegisterAsync<MyCommand, InboxAsyncB.CollidingInboxHandler>();

            var container = new ServiceCollection();
            container.AddTransient<InboxAsyncA.CollidingInboxHandler>();
            container.AddTransient<InboxAsyncB.CollidingInboxHandler>();
            container.AddTransient<MyValidationHandlerAsync<MyCommand>>();
            container.AddSingleton(inbox);
            container.AddTransient<UseInboxHandlerAsync<MyCommand>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });

            var factory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());
            var inboxConfiguration = new InboxConfiguration();

            return (
                new PipelineBuilder<MyCommand>(registryA, (IAmAHandlerFactoryAsync)factory, inboxConfiguration),
                new PipelineBuilder<MyCommand>(registryB, (IAmAHandlerFactoryAsync)factory, inboxConfiguration));
        }

        private static void AssertCacheExcludesUseInbox(Type handlerType)
        {
            IReadOnlyCollection<RequestHandlerAttribute> pre = GetCachedAttributes("s_preAttributesMemento", handlerType);
            IReadOnlyCollection<RequestHandlerAttribute> post = GetCachedAttributes("s_postAttributesMemento", handlerType);

            Assert.DoesNotContain(pre, a => a is UseInboxAttribute or UseInboxAsyncAttribute);
            Assert.DoesNotContain(post, a => a is UseInboxAttribute or UseInboxAsyncAttribute);
        }

        private static IReadOnlyCollection<RequestHandlerAttribute> GetCachedAttributes(string fieldName, Type handlerType)
        {
            FieldInfo? field = typeof(PipelineBuilder<MyCommand>).GetField(
                fieldName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(field);

            var cache = (IDictionary)field!.GetValue(null)!;
            Assert.True(cache.Contains(handlerType), $"Expected a cached memento entry keyed by {handlerType}.");
            return ((IEnumerable<RequestHandlerAttribute>)cache[handlerType]!).ToList();
        }

        private static PipelineTracer TracePipeline(IHandleRequests<MyCommand> firstInPipeline)
        {
            var pipelineTracer = new PipelineTracer();
            firstInPipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }

        private static PipelineTracer TracePipeline(IHandleRequestsAsync<MyCommand> firstInPipeline)
        {
            var pipelineTracer = new PipelineTracer();
            firstInPipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }
    }
}

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline.InboxTypeKeyed.SyncA
{
    using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;

    internal sealed class CollidingInboxHandler : RequestHandler<MyCommand>
    {
        [MyPreValidationHandler(1, HandlerTiming.Before)]
        public override MyCommand Handle(MyCommand command) => base.Handle(command);
    }
}

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline.InboxTypeKeyed.SyncB
{
    using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;

    internal sealed class CollidingInboxHandler : RequestHandler<MyCommand>
    {
        [MyPreValidationHandler(1, HandlerTiming.Before)]
        public override MyCommand Handle(MyCommand command) => base.Handle(command);
    }
}

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline.InboxTypeKeyed.AsyncA
{
    using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;

    internal sealed class CollidingInboxHandler : RequestHandlerAsync<MyCommand>
    {
        [MyPreValidationHandlerAsync(1, HandlerTiming.Before)]
        public override Task<MyCommand> HandleAsync(MyCommand command, CancellationToken cancellationToken = default)
            => base.HandleAsync(command, cancellationToken);
    }
}

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline.InboxTypeKeyed.AsyncB
{
    using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;

    internal sealed class CollidingInboxHandler : RequestHandlerAsync<MyCommand>
    {
        [MyPreValidationHandlerAsync(1, HandlerTiming.Before)]
        public override Task<MyCommand> HandleAsync(MyCommand command, CancellationToken cancellationToken = default)
            => base.HandleAsync(command, cancellationToken);
    }
}
