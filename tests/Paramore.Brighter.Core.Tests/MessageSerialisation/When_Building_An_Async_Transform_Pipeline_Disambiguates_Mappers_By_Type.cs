using System;
using Paramore.Brighter.Observability;
using A = Paramore.Brighter.Core.Tests.MessageSerialisation.AsyncTransformTypeKeyed.A;
using B = Paramore.Brighter.Core.Tests.MessageSerialisation.AsyncTransformTypeKeyed.B;
using Reuse = Paramore.Brighter.Core.Tests.MessageSerialisation.AsyncTransformTypeKeyed.Reuse;
using Scenarios = Paramore.Brighter.Core.Tests.MessageSerialisation.AsyncTransformTypeKeyed.Scenarios;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation
{
    public class When_Building_An_Async_Transform_Pipeline_Disambiguates_Mappers_By_Type
    {
        [Test]
        public async Task When_two_async_mappers_share_a_simple_name_each_wrap_pipeline_should_build_with_its_own_transforms_first_built_first()
        {
            // Arrange
            TransformPipelineBuilderAsync builder = CreateCollidingBuilder<Scenarios.WrapFirstBuiltFirst>();

            // Act — build A (FirstTransformAsync) first, warming the cache, then B (SecondTransformAsync)
            builder.BuildWrapPipeline<A.EventCommand<Scenarios.WrapFirstBuiltFirst>>();
            WrapPipelineAsync<B.EventCommand<Scenarios.WrapFirstBuiltFirst>> pipelineB =
                builder.BuildWrapPipeline<B.EventCommand<Scenarios.WrapFirstBuiltFirst>>();

            // Assert — B's wrap pipeline carries its own transform, never A's
            string trace = Trace(pipelineB).ToString();
            await Assert.That(trace).Contains("SecondTransformAsync");
            await Assert.That(trace).DoesNotContain("FirstTransformAsync");
        }

        [Test]
        public async Task When_two_async_mappers_share_a_simple_name_each_wrap_pipeline_should_build_with_its_own_transforms_opposite_order()
        {
            // Arrange
            TransformPipelineBuilderAsync builder = CreateCollidingBuilder<Scenarios.WrapOppositeOrder>();

            // Act — build B (SecondTransformAsync) first this time, then A (FirstTransformAsync)
            builder.BuildWrapPipeline<B.EventCommand<Scenarios.WrapOppositeOrder>>();
            WrapPipelineAsync<A.EventCommand<Scenarios.WrapOppositeOrder>> pipelineA =
                builder.BuildWrapPipeline<A.EventCommand<Scenarios.WrapOppositeOrder>>();

            // Assert — A's wrap pipeline carries its own transform, never B's
            string trace = Trace(pipelineA).ToString();
            await Assert.That(trace).Contains("FirstTransformAsync");
            await Assert.That(trace).DoesNotContain("SecondTransformAsync");
        }

        [Test]
        public async Task When_two_async_mappers_share_a_simple_name_each_unwrap_pipeline_should_build_with_its_own_transforms_first_built_first()
        {
            // Arrange
            TransformPipelineBuilderAsync builder = CreateCollidingBuilder<Scenarios.UnwrapFirstBuiltFirst>();

            // Act — build A (FirstTransformAsync) first, warming the cache, then B (SecondTransformAsync)
            builder.BuildUnwrapPipeline<A.EventCommand<Scenarios.UnwrapFirstBuiltFirst>>();
            UnwrapPipelineAsync<B.EventCommand<Scenarios.UnwrapFirstBuiltFirst>> pipelineB =
                builder.BuildUnwrapPipeline<B.EventCommand<Scenarios.UnwrapFirstBuiltFirst>>();

            // Assert — B's unwrap pipeline carries its own transform, never A's
            string trace = Trace(pipelineB).ToString();
            await Assert.That(trace).Contains("SecondTransformAsync");
            await Assert.That(trace).DoesNotContain("FirstTransformAsync");
        }

        [Test]
        public async Task When_two_async_mappers_share_a_simple_name_each_unwrap_pipeline_should_build_with_its_own_transforms_opposite_order()
        {
            // Arrange
            TransformPipelineBuilderAsync builder = CreateCollidingBuilder<Scenarios.UnwrapOppositeOrder>();

            // Act — build B (SecondTransformAsync) first this time, then A (FirstTransformAsync)
            builder.BuildUnwrapPipeline<B.EventCommand<Scenarios.UnwrapOppositeOrder>>();
            UnwrapPipelineAsync<A.EventCommand<Scenarios.UnwrapOppositeOrder>> pipelineA =
                builder.BuildUnwrapPipeline<A.EventCommand<Scenarios.UnwrapOppositeOrder>>();

            // Assert — A's unwrap pipeline carries its own transform, never B's
            string trace = Trace(pipelineA).ToString();
            await Assert.That(trace).Contains("FirstTransformAsync");
            await Assert.That(trace).DoesNotContain("SecondTransformAsync");
        }

        [Test]
        public async Task When_a_single_async_mapper_is_built_twice_should_produce_the_same_transform_pipelines()
        {
            // Arrange
            var registry = new MessageMapperRegistry(
                null,
                new SimpleMessageMapperFactoryAsync(_ => new Reuse.ReuseMapper()));
            registry.RegisterAsync<Reuse.ReuseCommand, Reuse.ReuseMapper>();

            var transformerFactory = new SimpleMessageTransformerFactoryAsync(_ => new Reuse.ReuseTransformAsync());
            var builder = new TransformPipelineBuilderAsync(registry, transformerFactory, InstrumentationOptions.All);

            // Act
            string firstWrap = Trace(builder.BuildWrapPipeline<Reuse.ReuseCommand>()).ToString();
            string secondWrap = Trace(builder.BuildWrapPipeline<Reuse.ReuseCommand>()).ToString();
            string firstUnwrap = Trace(builder.BuildUnwrapPipeline<Reuse.ReuseCommand>()).ToString();
            string secondUnwrap = Trace(builder.BuildUnwrapPipeline<Reuse.ReuseCommand>()).ToString();

            // Assert
            await Assert.That(secondWrap).IsEqualTo(firstWrap);
            await Assert.That(secondUnwrap).IsEqualTo(firstUnwrap);
        }

        private static TransformPipelineBuilderAsync CreateCollidingBuilder<TScenario>()
        {
            var registry = new MessageMapperRegistry(
                null,
                new SimpleMessageMapperFactoryAsync(t =>
                    t == typeof(A.CollidingMapper<TScenario>)
                        ? new A.CollidingMapper<TScenario>()
                        : (IAmAMessageMapperAsync)new B.CollidingMapper<TScenario>()));
            registry.RegisterAsync<A.EventCommand<TScenario>, A.CollidingMapper<TScenario>>();
            registry.RegisterAsync<B.EventCommand<TScenario>, B.CollidingMapper<TScenario>>();

            var transformerFactory = new SimpleMessageTransformerFactoryAsync(t =>
                t == typeof(A.FirstTransformAsync)
                    ? new A.FirstTransformAsync()
                    : (IAmAMessageTransformAsync)new B.SecondTransformAsync());

            return new TransformPipelineBuilderAsync(registry, transformerFactory, InstrumentationOptions.All);
        }

        private static TransformPipelineTracer Trace<TRequest>(WrapPipelineAsync<TRequest> pipeline)
            where TRequest : class, IRequest
        {
            var pipelineTracer = new TransformPipelineTracer();
            pipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }

        private static TransformPipelineTracer Trace<TRequest>(UnwrapPipelineAsync<TRequest> pipeline)
            where TRequest : class, IRequest
        {
            var pipelineTracer = new TransformPipelineTracer();
            pipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }

    }
}

namespace Paramore.Brighter.Core.Tests.MessageSerialisation.AsyncTransformTypeKeyed.A
{
    using System;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
    using Paramore.Brighter.Extensions;

    public sealed class EventCommand<TScenario> : Command
    {
        public EventCommand() : base(Guid.NewGuid()) { }
    }

    internal sealed class FirstTransformAsync : TransformAsync
    {
        public override Task<Message> WrapAsync(Message message, Publication publication, CancellationToken cancellationToken)
            => Task.FromResult(message);

        public override Task<Message> UnwrapAsync(Message message, CancellationToken cancellationToken)
            => Task.FromResult(message);
    }

    internal sealed class FirstWrapWith : WrapWithAttribute
    {
        public FirstWrapWith(int step) : base(step) { }

        public override Type GetHandlerType() => typeof(FirstTransformAsync);
    }

    internal sealed class FirstUnwrapWith : UnwrapWithAttribute
    {
        public FirstUnwrapWith(int step) : base(step) { }

        public override Type GetHandlerType() => typeof(FirstTransformAsync);
    }

    internal sealed class CollidingMapper<TScenario> : IAmAMessageMapperAsync<EventCommand<TScenario>>
    {
        public IRequestContext Context { get; set; }

        [FirstWrapWith(0)]
        public Task<Message> MapToMessageAsync(EventCommand<TScenario> request, Publication publication, CancellationToken cancellationToken = default)
            => Task.FromResult(new Message(
                new MessageHeader(request.Id, publication.Topic, request.RequestToMessageType(), timeStamp: DateTime.UtcNow),
                new MessageBody(JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.General)))));

        [FirstUnwrapWith(0)]
        public Task<EventCommand<TScenario>> MapToRequestAsync(Message message, CancellationToken cancellationToken = default)
            => Task.FromResult(JsonSerializer.Deserialize<EventCommand<TScenario>>(message.Body.Value));
    }
}

namespace Paramore.Brighter.Core.Tests.MessageSerialisation.AsyncTransformTypeKeyed.B
{
    using System;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
    using Paramore.Brighter.Extensions;

    public sealed class EventCommand<TScenario> : Command
    {
        public EventCommand() : base(Guid.NewGuid()) { }
    }

    internal sealed class SecondTransformAsync : TransformAsync
    {
        public override Task<Message> WrapAsync(Message message, Publication publication, CancellationToken cancellationToken)
            => Task.FromResult(message);

        public override Task<Message> UnwrapAsync(Message message, CancellationToken cancellationToken)
            => Task.FromResult(message);
    }

    internal sealed class SecondWrapWith : WrapWithAttribute
    {
        public SecondWrapWith(int step) : base(step) { }

        public override Type GetHandlerType() => typeof(SecondTransformAsync);
    }

    internal sealed class SecondUnwrapWith : UnwrapWithAttribute
    {
        public SecondUnwrapWith(int step) : base(step) { }

        public override Type GetHandlerType() => typeof(SecondTransformAsync);
    }

    internal sealed class CollidingMapper<TScenario> : IAmAMessageMapperAsync<EventCommand<TScenario>>
    {
        public IRequestContext Context { get; set; }

        [SecondWrapWith(0)]
        public Task<Message> MapToMessageAsync(EventCommand<TScenario> request, Publication publication, CancellationToken cancellationToken = default)
            => Task.FromResult(new Message(
                new MessageHeader(request.Id, publication.Topic, request.RequestToMessageType(), timeStamp: DateTime.UtcNow),
                new MessageBody(JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.General)))));

        [SecondUnwrapWith(0)]
        public Task<EventCommand<TScenario>> MapToRequestAsync(Message message, CancellationToken cancellationToken = default)
            => Task.FromResult(JsonSerializer.Deserialize<EventCommand<TScenario>>(message.Body.Value));
    }
}

namespace Paramore.Brighter.Core.Tests.MessageSerialisation.AsyncTransformTypeKeyed.Scenarios
{
    internal sealed class WrapFirstBuiltFirst { }
    internal sealed class WrapOppositeOrder { }
    internal sealed class UnwrapFirstBuiltFirst { }
    internal sealed class UnwrapOppositeOrder { }
}

namespace Paramore.Brighter.Core.Tests.MessageSerialisation.AsyncTransformTypeKeyed.Reuse
{
    using System;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
    using Paramore.Brighter.Extensions;

    public sealed class ReuseCommand : Command
    {
        public ReuseCommand() : base(Guid.NewGuid()) { }
    }

    internal sealed class ReuseTransformAsync : TransformAsync
    {
        public override Task<Message> WrapAsync(Message message, Publication publication, CancellationToken cancellationToken)
            => Task.FromResult(message);

        public override Task<Message> UnwrapAsync(Message message, CancellationToken cancellationToken)
            => Task.FromResult(message);
    }

    internal sealed class ReuseWrapWith : WrapWithAttribute
    {
        public ReuseWrapWith(int step) : base(step) { }

        public override Type GetHandlerType() => typeof(ReuseTransformAsync);
    }

    internal sealed class ReuseUnwrapWith : UnwrapWithAttribute
    {
        public ReuseUnwrapWith(int step) : base(step) { }

        public override Type GetHandlerType() => typeof(ReuseTransformAsync);
    }

    internal sealed class ReuseMapper : IAmAMessageMapperAsync<ReuseCommand>
    {
        public IRequestContext Context { get; set; }

        [ReuseWrapWith(0)]
        public Task<Message> MapToMessageAsync(ReuseCommand request, Publication publication, CancellationToken cancellationToken = default)
            => Task.FromResult(new Message(
                new MessageHeader(request.Id, publication.Topic, request.RequestToMessageType(), timeStamp: DateTime.UtcNow),
                new MessageBody(JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.General)))));

        [ReuseUnwrapWith(0)]
        public Task<ReuseCommand> MapToRequestAsync(Message message, CancellationToken cancellationToken = default)
            => Task.FromResult(JsonSerializer.Deserialize<ReuseCommand>(message.Body.Value));
    }
}
