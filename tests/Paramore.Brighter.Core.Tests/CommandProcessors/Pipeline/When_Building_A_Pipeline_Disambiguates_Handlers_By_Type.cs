using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using SyncA = Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline.TypeKeyed.SyncA;
using SyncB = Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline.TypeKeyed.SyncB;
using AsyncA = Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline.TypeKeyed.AsyncA;
using AsyncB = Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline.TypeKeyed.AsyncB;
using Reuse = Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline.TypeKeyed.Reuse;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline
{
    public class When_Building_A_Pipeline_Disambiguates_Handlers_By_Type
    {
        [Test]
        public async Task When_two_sync_handlers_share_a_simple_name_each_should_build_with_its_own_decorators_winner_built_first()
        {
            // Arrange
            PipelineBuilder<MyCommand>.ClearPipelineCache();
            (PipelineBuilder<MyCommand> builderA, PipelineBuilder<MyCommand> builderB) = CreateSyncBuilders();

            // Act — build A (pre/Validation decorator) first, warming the cache, then B (post/Logging decorator)
            builderA.Build(new MyCommand(), new RequestContext()).First();
            IHandleRequests<MyCommand> pipelineB = builderB.Build(new MyCommand(), new RequestContext()).First();

            // Assert — B carries its own post decorator (Logging), never A's pre decorator (Validation)
            string trace = TracePipeline(pipelineB).ToString();
            await Assert.That(trace).Contains("MyLoggingHandler");
            await Assert.That(trace).DoesNotContain("MyValidationHandler");
        }

        [Test]
        public async Task When_two_sync_handlers_share_a_simple_name_each_should_build_with_its_own_decorators_opposite_order()
        {
            // Arrange
            PipelineBuilder<MyCommand>.ClearPipelineCache();
            (PipelineBuilder<MyCommand> builderA, PipelineBuilder<MyCommand> builderB) = CreateSyncBuilders();

            // Act — build B (post/Logging decorator) first this time, then A (pre/Validation decorator)
            builderB.Build(new MyCommand(), new RequestContext()).First();
            IHandleRequests<MyCommand> pipelineA = builderA.Build(new MyCommand(), new RequestContext()).First();

            // Assert — A carries its own pre decorator (Validation), never B's post decorator (Logging)
            string trace = TracePipeline(pipelineA).ToString();
            await Assert.That(trace).Contains("MyValidationHandler");
            await Assert.That(trace).DoesNotContain("MyLoggingHandler");
        }

        [Test]
        public async Task When_two_async_handlers_share_a_simple_name_each_should_build_with_its_own_decorators_winner_built_first()
        {
            // Arrange
            PipelineBuilder<MyCommand>.ClearPipelineCache();
            (PipelineBuilder<MyCommand> builderA, PipelineBuilder<MyCommand> builderB) = CreateAsyncBuilders();

            // Act — build async A (pre/Validation) first, then async B (post/Logging)
            builderA.BuildAsync(new MyCommand(), new RequestContext(), false).First();
            IHandleRequestsAsync<MyCommand> pipelineB =
                builderB.BuildAsync(new MyCommand(), new RequestContext(), false).First();

            // Assert — async B carries its own post decorator, never async A's pre decorator
            string trace = TracePipeline(pipelineB).ToString();
            await Assert.That(trace).Contains("MyLoggingHandlerAsync");
            await Assert.That(trace).DoesNotContain("MyValidationHandlerAsync");
        }

        [Test]
        public async Task When_two_async_handlers_share_a_simple_name_each_should_build_with_its_own_decorators_opposite_order()
        {
            // Arrange
            PipelineBuilder<MyCommand>.ClearPipelineCache();
            (PipelineBuilder<MyCommand> builderA, PipelineBuilder<MyCommand> builderB) = CreateAsyncBuilders();

            // Act — build async B (post/Logging) first this time, then async A (pre/Validation)
            builderB.BuildAsync(new MyCommand(), new RequestContext(), false).First();
            IHandleRequestsAsync<MyCommand> pipelineA =
                builderA.BuildAsync(new MyCommand(), new RequestContext(), false).First();

            // Assert — async A carries its own pre decorator, never async B's post decorator
            string trace = TracePipeline(pipelineA).ToString();
            await Assert.That(trace).Contains("MyValidationHandlerAsync");
            await Assert.That(trace).DoesNotContain("MyLoggingHandlerAsync");
        }

        [Test]
        public async Task When_a_single_handler_is_built_twice_should_leave_one_cache_entry_keyed_by_its_runtime_type()
        {
            // Arrange — a request type unique to this fact, so the process-global per-closed-generic
            // mementos hold exactly this handler's entry and the count assertion stays deterministic
            PipelineBuilder<Reuse.ReuseCommand>.ClearPipelineCache();

            var registry = new SubscriberRegistry();
            registry.Register<Reuse.ReuseCommand, Reuse.ReuseHandler>();

            var container = new ServiceCollection();
            container.AddTransient<Reuse.ReuseHandler>();
            container.AddTransient<MyValidationHandler<Reuse.ReuseCommand>>();
            container.AddTransient<MyLoggingHandler<Reuse.ReuseCommand>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });

            var factory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());
            var builder = new PipelineBuilder<Reuse.ReuseCommand>(registry, (IAmAHandlerFactorySync)factory);

            // Act — build the same handler twice (single-threaded)
            string firstTrace =
                TracePipeline(builder.Build(new Reuse.ReuseCommand(), new RequestContext()).First()).ToString();
            string secondTrace =
                TracePipeline(builder.Build(new Reuse.ReuseCommand(), new RequestContext()).First()).ToString();

            // Assert — exactly one entry per memento, keyed by the handler's runtime Type, and the
            // second build's decorator sequence is equivalent to the first
            IReadOnlyCollection<System.Type> preKeys = await GetMementoKeys<Reuse.ReuseCommand>("s_preAttributesMemento");
            IReadOnlyCollection<System.Type> postKeys = await GetMementoKeys<Reuse.ReuseCommand>("s_postAttributesMemento");

            await Assert.That(preKeys).IsEqualTo(new[] { typeof(Reuse.ReuseHandler) });
            await Assert.That(postKeys).IsEqualTo(new[] { typeof(Reuse.ReuseHandler) });
            await Assert.That(secondTrace).IsEqualTo(firstTrace);
        }

        private static (PipelineBuilder<MyCommand>, PipelineBuilder<MyCommand>) CreateSyncBuilders()
        {
            var registryA = new SubscriberRegistry();
            registryA.Register<MyCommand, SyncA.CollidingHandler>();

            var registryB = new SubscriberRegistry();
            registryB.Register<MyCommand, SyncB.CollidingHandler>();

            var container = new ServiceCollection();
            container.AddTransient<SyncA.CollidingHandler>();
            container.AddTransient<SyncB.CollidingHandler>();
            container.AddTransient<MyValidationHandler<MyCommand>>();
            container.AddTransient<MyLoggingHandler<MyCommand>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });

            var factory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());

            return (
                new PipelineBuilder<MyCommand>(registryA, (IAmAHandlerFactorySync)factory),
                new PipelineBuilder<MyCommand>(registryB, (IAmAHandlerFactorySync)factory));
        }

        private static (PipelineBuilder<MyCommand>, PipelineBuilder<MyCommand>) CreateAsyncBuilders()
        {
            var registryA = new SubscriberRegistry();
            registryA.RegisterAsync<MyCommand, AsyncA.CollidingHandler>();

            var registryB = new SubscriberRegistry();
            registryB.RegisterAsync<MyCommand, AsyncB.CollidingHandler>();

            var container = new ServiceCollection();
            container.AddTransient<AsyncA.CollidingHandler>();
            container.AddTransient<AsyncB.CollidingHandler>();
            container.AddTransient<MyValidationHandlerAsync<MyCommand>>();
            container.AddTransient<MyLoggingHandlerAsync<MyCommand>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });

            var factory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());

            return (
                new PipelineBuilder<MyCommand>(registryA, (IAmAHandlerFactoryAsync)factory),
                new PipelineBuilder<MyCommand>(registryB, (IAmAHandlerFactoryAsync)factory));
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

        private static PipelineTracer TracePipeline(IHandleRequests<Reuse.ReuseCommand> firstInPipeline)
        {
            var pipelineTracer = new PipelineTracer();
            firstInPipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }

        private static async Task<IReadOnlyCollection<System.Type>> GetMementoKeys<TRequest>(string fieldName)
            where TRequest : class, IRequest
        {
            FieldInfo? field = typeof(PipelineBuilder<TRequest>).GetField(
                fieldName,
                BindingFlags.Static | BindingFlags.NonPublic);
            await Assert.That(field).IsNotNull();

            var cache = (IDictionary)field!.GetValue(null)!;
            return cache.Keys.Cast<System.Type>().ToList();
        }
    }
}

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline.TypeKeyed.SyncA
{
    using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;

    internal sealed class CollidingHandler : RequestHandler<MyCommand>
    {
        [MyPreValidationHandler(1, HandlerTiming.Before)]
        public override MyCommand Handle(MyCommand command) => base.Handle(command);
    }
}

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline.TypeKeyed.SyncB
{
    using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;

    internal sealed class CollidingHandler : RequestHandler<MyCommand>
    {
        [MyPostLoggingHandler(1, HandlerTiming.After)]
        public override MyCommand Handle(MyCommand command) => base.Handle(command);
    }
}

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline.TypeKeyed.AsyncA
{
    using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;

    internal sealed class CollidingHandler : RequestHandlerAsync<MyCommand>
    {
        [MyPreValidationHandlerAsync(1, HandlerTiming.Before)]
        public override Task<MyCommand> HandleAsync(MyCommand command, CancellationToken cancellationToken = default)
            => base.HandleAsync(command, cancellationToken);
    }
}

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline.TypeKeyed.AsyncB
{
    using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;

    internal sealed class CollidingHandler : RequestHandlerAsync<MyCommand>
    {
        [MyPostLoggingHandlerAsync(1, HandlerTiming.After)]
        public override Task<MyCommand> HandleAsync(MyCommand command, CancellationToken cancellationToken = default)
            => base.HandleAsync(command, cancellationToken);
    }
}

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline.TypeKeyed.Reuse
{
    using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;

    public sealed class ReuseCommand() : Command(Id.Random()) { }

    internal sealed class ReuseHandler : RequestHandler<ReuseCommand>
    {
        [MyPreValidationHandler(2, HandlerTiming.Before)]
        [MyPostLoggingHandler(1, HandlerTiming.After)]
        public override ReuseCommand Handle(ReuseCommand command) => base.Handle(command);
    }
}
