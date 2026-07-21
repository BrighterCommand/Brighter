using System.Linq;
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
using Scenarios = Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline.TypeKeyed.Scenarios;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline
{
    public class When_Building_A_Pipeline_Disambiguates_Handlers_By_Type
    {
        [Test]
        public async Task When_two_sync_handlers_share_a_simple_name_each_should_build_with_its_own_decorators_winner_built_first()
        {
            // Arrange
            (PipelineBuilder<Scenarios.SyncFirstBuiltFirstCommand> builderA,
                PipelineBuilder<Scenarios.SyncFirstBuiltFirstCommand> builderB) =
                CreateSyncBuilders<Scenarios.SyncFirstBuiltFirstCommand>();

            // Act — build A (pre/Validation decorator) first, warming the cache, then B (post/Logging decorator)
            builderA.Build(new Scenarios.SyncFirstBuiltFirstCommand(), new RequestContext()).First();
            IHandleRequests<Scenarios.SyncFirstBuiltFirstCommand> pipelineB = builderB
                .Build(new Scenarios.SyncFirstBuiltFirstCommand(), new RequestContext()).First();

            // Assert — B carries its own post decorator (Logging), never A's pre decorator (Validation)
            string trace = TracePipeline(pipelineB).ToString();
            await Assert.That(trace).Contains("MyLoggingHandler");
            await Assert.That(trace).DoesNotContain("MyValidationHandler");
        }

        [Test]
        public async Task When_two_sync_handlers_share_a_simple_name_each_should_build_with_its_own_decorators_opposite_order()
        {
            // Arrange
            (PipelineBuilder<Scenarios.SyncOppositeOrderCommand> builderA,
                PipelineBuilder<Scenarios.SyncOppositeOrderCommand> builderB) =
                CreateSyncBuilders<Scenarios.SyncOppositeOrderCommand>();

            // Act — build B (post/Logging decorator) first this time, then A (pre/Validation decorator)
            builderB.Build(new Scenarios.SyncOppositeOrderCommand(), new RequestContext()).First();
            IHandleRequests<Scenarios.SyncOppositeOrderCommand> pipelineA = builderA
                .Build(new Scenarios.SyncOppositeOrderCommand(), new RequestContext()).First();

            // Assert — A carries its own pre decorator (Validation), never B's post decorator (Logging)
            string trace = TracePipeline(pipelineA).ToString();
            await Assert.That(trace).Contains("MyValidationHandler");
            await Assert.That(trace).DoesNotContain("MyLoggingHandler");
        }

        [Test]
        public async Task When_two_async_handlers_share_a_simple_name_each_should_build_with_its_own_decorators_winner_built_first()
        {
            // Arrange
            (PipelineBuilder<Scenarios.AsyncFirstBuiltFirstCommand> builderA,
                PipelineBuilder<Scenarios.AsyncFirstBuiltFirstCommand> builderB) =
                CreateAsyncBuilders<Scenarios.AsyncFirstBuiltFirstCommand>();

            // Act — build async A (pre/Validation) first, then async B (post/Logging)
            builderA.BuildAsync(new Scenarios.AsyncFirstBuiltFirstCommand(), new RequestContext(), false).First();
            IHandleRequestsAsync<Scenarios.AsyncFirstBuiltFirstCommand> pipelineB = builderB
                .BuildAsync(new Scenarios.AsyncFirstBuiltFirstCommand(), new RequestContext(), false).First();

            // Assert — async B carries its own post decorator, never async A's pre decorator
            string trace = TracePipeline(pipelineB).ToString();
            await Assert.That(trace).Contains("MyLoggingHandlerAsync");
            await Assert.That(trace).DoesNotContain("MyValidationHandlerAsync");
        }

        [Test]
        public async Task When_two_async_handlers_share_a_simple_name_each_should_build_with_its_own_decorators_opposite_order()
        {
            // Arrange
            (PipelineBuilder<Scenarios.AsyncOppositeOrderCommand> builderA,
                PipelineBuilder<Scenarios.AsyncOppositeOrderCommand> builderB) =
                CreateAsyncBuilders<Scenarios.AsyncOppositeOrderCommand>();

            // Act — build async B (post/Logging) first this time, then async A (pre/Validation)
            builderB.BuildAsync(new Scenarios.AsyncOppositeOrderCommand(), new RequestContext(), false).First();
            IHandleRequestsAsync<Scenarios.AsyncOppositeOrderCommand> pipelineA = builderA
                .BuildAsync(new Scenarios.AsyncOppositeOrderCommand(), new RequestContext(), false).First();

            // Assert — async A carries its own pre decorator, never async B's post decorator
            string trace = TracePipeline(pipelineA).ToString();
            await Assert.That(trace).Contains("MyValidationHandlerAsync");
            await Assert.That(trace).DoesNotContain("MyLoggingHandlerAsync");
        }

        [Test]
        public async Task When_a_single_handler_is_built_twice_should_produce_the_same_pipeline()
        {
            // Arrange
            var registry = new SubscriberRegistry();
            registry.Register<Reuse.ReuseCommand, Reuse.ReuseHandler>();

            var container = new ServiceCollection();
            container.AddTransient<Reuse.ReuseHandler>();
            container.AddTransient<MyValidationHandler<Reuse.ReuseCommand>>();
            container.AddTransient<MyLoggingHandler<Reuse.ReuseCommand>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });

            var factory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());
            var builder = new PipelineBuilder<Reuse.ReuseCommand>(registry, (IAmAHandlerFactorySync)factory);

            // Act
            string firstTrace =
                TracePipeline(builder.Build(new Reuse.ReuseCommand(), new RequestContext()).First()).ToString();
            string secondTrace =
                TracePipeline(builder.Build(new Reuse.ReuseCommand(), new RequestContext()).First()).ToString();

            // Assert
            await Assert.That(secondTrace).IsEqualTo(firstTrace);
        }

        private static (PipelineBuilder<TCommand>, PipelineBuilder<TCommand>) CreateSyncBuilders<TCommand>()
            where TCommand : MyCommand
        {
            var registryA = new SubscriberRegistry();
            registryA.Register<TCommand, SyncA.CollidingHandler<TCommand>>();

            var registryB = new SubscriberRegistry();
            registryB.Register<TCommand, SyncB.CollidingHandler<TCommand>>();

            var container = new ServiceCollection();
            container.AddTransient<SyncA.CollidingHandler<TCommand>>();
            container.AddTransient<SyncB.CollidingHandler<TCommand>>();
            container.AddTransient<MyValidationHandler<TCommand>>();
            container.AddTransient<MyLoggingHandler<TCommand>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });

            var factory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());

            return (
                new PipelineBuilder<TCommand>(registryA, (IAmAHandlerFactorySync)factory),
                new PipelineBuilder<TCommand>(registryB, (IAmAHandlerFactorySync)factory));
        }

        private static (PipelineBuilder<TCommand>, PipelineBuilder<TCommand>) CreateAsyncBuilders<TCommand>()
            where TCommand : MyCommand
        {
            var registryA = new SubscriberRegistry();
            registryA.RegisterAsync<TCommand, AsyncA.CollidingHandler<TCommand>>();

            var registryB = new SubscriberRegistry();
            registryB.RegisterAsync<TCommand, AsyncB.CollidingHandler<TCommand>>();

            var container = new ServiceCollection();
            container.AddTransient<AsyncA.CollidingHandler<TCommand>>();
            container.AddTransient<AsyncB.CollidingHandler<TCommand>>();
            container.AddTransient<MyValidationHandlerAsync<TCommand>>();
            container.AddTransient<MyLoggingHandlerAsync<TCommand>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });

            var factory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());

            return (
                new PipelineBuilder<TCommand>(registryA, (IAmAHandlerFactoryAsync)factory),
                new PipelineBuilder<TCommand>(registryB, (IAmAHandlerFactoryAsync)factory));
        }

        private static PipelineTracer TracePipeline<TCommand>(IHandleRequests<TCommand> firstInPipeline)
            where TCommand : class, IRequest
        {
            var pipelineTracer = new PipelineTracer();
            firstInPipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }

        private static PipelineTracer TracePipeline<TCommand>(IHandleRequestsAsync<TCommand> firstInPipeline)
            where TCommand : class, IRequest
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

    }
}

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline.TypeKeyed.SyncA
{
    using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;

    internal sealed class CollidingHandler<TCommand> : RequestHandler<TCommand>
        where TCommand : MyCommand
    {
        [MyPreValidationHandler(1, HandlerTiming.Before)]
        public override TCommand Handle(TCommand command) => base.Handle(command);
    }
}

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline.TypeKeyed.SyncB
{
    using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;

    internal sealed class CollidingHandler<TCommand> : RequestHandler<TCommand>
        where TCommand : MyCommand
    {
        [MyPostLoggingHandler(1, HandlerTiming.After)]
        public override TCommand Handle(TCommand command) => base.Handle(command);
    }
}

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline.TypeKeyed.AsyncA
{
    using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;

    internal sealed class CollidingHandler<TCommand> : RequestHandlerAsync<TCommand>
        where TCommand : MyCommand
    {
        [MyPreValidationHandlerAsync(1, HandlerTiming.Before)]
        public override Task<TCommand> HandleAsync(TCommand command, CancellationToken cancellationToken = default)
            => base.HandleAsync(command, cancellationToken);
    }
}

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline.TypeKeyed.AsyncB
{
    using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;

    internal sealed class CollidingHandler<TCommand> : RequestHandlerAsync<TCommand>
        where TCommand : MyCommand
    {
        [MyPostLoggingHandlerAsync(1, HandlerTiming.After)]
        public override Task<TCommand> HandleAsync(TCommand command, CancellationToken cancellationToken = default)
            => base.HandleAsync(command, cancellationToken);
    }
}

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline.TypeKeyed.Scenarios
{
    using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;

    internal sealed class SyncFirstBuiltFirstCommand : MyCommand { }
    internal sealed class SyncOppositeOrderCommand : MyCommand { }
    internal sealed class AsyncFirstBuiltFirstCommand : MyCommand { }
    internal sealed class AsyncOppositeOrderCommand : MyCommand { }
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
