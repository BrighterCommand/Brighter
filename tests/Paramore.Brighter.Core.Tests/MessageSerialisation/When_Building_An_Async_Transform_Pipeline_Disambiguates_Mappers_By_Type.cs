using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Paramore.Brighter.Observability;
using Xunit;
using A = Paramore.Brighter.Core.Tests.MessageSerialisation.AsyncTransformTypeKeyed.A;
using B = Paramore.Brighter.Core.Tests.MessageSerialisation.AsyncTransformTypeKeyed.B;
using Reuse = Paramore.Brighter.Core.Tests.MessageSerialisation.AsyncTransformTypeKeyed.Reuse;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation
{
    public class When_Building_An_Async_Transform_Pipeline_Disambiguates_Mappers_By_Type
    {
        [Fact]
        public void When_two_async_mappers_share_a_simple_name_each_wrap_pipeline_should_build_with_its_own_transforms_first_built_first()
        {
            // Arrange
            TransformPipelineBuilderAsync.ClearPipelineCache();
            TransformPipelineBuilderAsync builder = CreateCollidingBuilder();

            // Act — build A (FirstTransformAsync) first, warming the cache, then B (SecondTransformAsync)
            builder.BuildWrapPipeline<A.EventCommand>();
            WrapPipelineAsync<B.EventCommand> pipelineB = builder.BuildWrapPipeline<B.EventCommand>();

            // Assert — B's wrap pipeline carries its own transform, never A's
            string trace = Trace(pipelineB).ToString();
            Assert.Contains("SecondTransformAsync", trace);
            Assert.DoesNotContain("FirstTransformAsync", trace);
        }

        [Fact]
        public void When_two_async_mappers_share_a_simple_name_each_wrap_pipeline_should_build_with_its_own_transforms_opposite_order()
        {
            // Arrange
            TransformPipelineBuilderAsync.ClearPipelineCache();
            TransformPipelineBuilderAsync builder = CreateCollidingBuilder();

            // Act — build B (SecondTransformAsync) first this time, then A (FirstTransformAsync)
            builder.BuildWrapPipeline<B.EventCommand>();
            WrapPipelineAsync<A.EventCommand> pipelineA = builder.BuildWrapPipeline<A.EventCommand>();

            // Assert — A's wrap pipeline carries its own transform, never B's
            string trace = Trace(pipelineA).ToString();
            Assert.Contains("FirstTransformAsync", trace);
            Assert.DoesNotContain("SecondTransformAsync", trace);
        }

        [Fact]
        public void When_two_async_mappers_share_a_simple_name_each_unwrap_pipeline_should_build_with_its_own_transforms_first_built_first()
        {
            // Arrange
            TransformPipelineBuilderAsync.ClearPipelineCache();
            TransformPipelineBuilderAsync builder = CreateCollidingBuilder();

            // Act — build A (FirstTransformAsync) first, warming the cache, then B (SecondTransformAsync)
            builder.BuildUnwrapPipeline<A.EventCommand>();
            UnwrapPipelineAsync<B.EventCommand> pipelineB = builder.BuildUnwrapPipeline<B.EventCommand>();

            // Assert — B's unwrap pipeline carries its own transform, never A's
            string trace = Trace(pipelineB).ToString();
            Assert.Contains("SecondTransformAsync", trace);
            Assert.DoesNotContain("FirstTransformAsync", trace);
        }

        [Fact]
        public void When_two_async_mappers_share_a_simple_name_each_unwrap_pipeline_should_build_with_its_own_transforms_opposite_order()
        {
            // Arrange
            TransformPipelineBuilderAsync.ClearPipelineCache();
            TransformPipelineBuilderAsync builder = CreateCollidingBuilder();

            // Act — build B (SecondTransformAsync) first this time, then A (FirstTransformAsync)
            builder.BuildUnwrapPipeline<B.EventCommand>();
            UnwrapPipelineAsync<A.EventCommand> pipelineA = builder.BuildUnwrapPipeline<A.EventCommand>();

            // Assert — A's unwrap pipeline carries its own transform, never B's
            string trace = Trace(pipelineA).ToString();
            Assert.Contains("FirstTransformAsync", trace);
            Assert.DoesNotContain("SecondTransformAsync", trace);
        }

        [Fact]
        public void When_a_single_async_mapper_is_built_twice_post_warmup_should_keep_one_entry_per_transform_cache_keyed_by_its_runtime_type()
        {
            // Arrange — a mapper/request unique to this fact, so the process-global mementos hold
            // exactly this mapper's entries and the count assertion stays deterministic
            TransformPipelineBuilderAsync.ClearPipelineCache();

            var registry = new MessageMapperRegistry(
                null,
                new SimpleMessageMapperFactoryAsync(_ => new Reuse.ReuseMapper()));
            registry.RegisterAsync<Reuse.ReuseCommand, Reuse.ReuseMapper>();

            var transformerFactory = new SimpleMessageTransformerFactoryAsync(_ => new Reuse.ReuseTransformAsync());
            var builder = new TransformPipelineBuilderAsync(registry, transformerFactory, InstrumentationOptions.All);

            // Act — build the same mapper's wrap and unwrap pipelines twice (single-threaded, so the
            // GetOrAdd factory has already run and the retained entry is served thereafter)
            string firstWrap = Trace(builder.BuildWrapPipeline<Reuse.ReuseCommand>()).ToString();
            string secondWrap = Trace(builder.BuildWrapPipeline<Reuse.ReuseCommand>()).ToString();
            string firstUnwrap = Trace(builder.BuildUnwrapPipeline<Reuse.ReuseCommand>()).ToString();
            string secondUnwrap = Trace(builder.BuildUnwrapPipeline<Reuse.ReuseCommand>()).ToString();

            // Assert — exactly one entry per memento, keyed by the mapper's runtime Type, and the
            // second build's transform sequence is equivalent to the first
            IReadOnlyCollection<Type> wrapKeys = GetMementoKeys("s_wrapTransformsMemento");
            IReadOnlyCollection<Type> unwrapKeys = GetMementoKeys("s_unWrapTransformsMemento");

            Assert.Equal(new[] { typeof(Reuse.ReuseMapper) }, wrapKeys);
            Assert.Equal(new[] { typeof(Reuse.ReuseMapper) }, unwrapKeys);
            Assert.Equal(firstWrap, secondWrap);
            Assert.Equal(firstUnwrap, secondUnwrap);
        }

        private static TransformPipelineBuilderAsync CreateCollidingBuilder()
        {
            var registry = new MessageMapperRegistry(
                null,
                new SimpleMessageMapperFactoryAsync(t =>
                    t == typeof(A.CollidingMapper)
                        ? new A.CollidingMapper()
                        : (IAmAMessageMapperAsync)new B.CollidingMapper()));
            registry.RegisterAsync<A.EventCommand, A.CollidingMapper>();
            registry.RegisterAsync<B.EventCommand, B.CollidingMapper>();

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

        private static IReadOnlyCollection<Type> GetMementoKeys(string fieldName)
        {
            FieldInfo? field = typeof(TransformPipelineBuilderAsync).GetField(
                fieldName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(field);

            var cache = (IDictionary)field!.GetValue(null)!;
            return cache.Keys.Cast<Type>().ToList();
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

    public sealed class EventCommand : Command
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

    internal sealed class CollidingMapper : IAmAMessageMapperAsync<EventCommand>
    {
        public IRequestContext Context { get; set; }

        [FirstWrapWith(0)]
        public Task<Message> MapToMessageAsync(EventCommand request, Publication publication, CancellationToken cancellationToken = default)
            => Task.FromResult(new Message(
                new MessageHeader(request.Id, publication.Topic, request.RequestToMessageType(), timeStamp: DateTime.UtcNow),
                new MessageBody(JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.General)))));

        [FirstUnwrapWith(0)]
        public Task<EventCommand> MapToRequestAsync(Message message, CancellationToken cancellationToken = default)
            => Task.FromResult(JsonSerializer.Deserialize<EventCommand>(message.Body.Value));
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

    public sealed class EventCommand : Command
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

    internal sealed class CollidingMapper : IAmAMessageMapperAsync<EventCommand>
    {
        public IRequestContext Context { get; set; }

        [SecondWrapWith(0)]
        public Task<Message> MapToMessageAsync(EventCommand request, Publication publication, CancellationToken cancellationToken = default)
            => Task.FromResult(new Message(
                new MessageHeader(request.Id, publication.Topic, request.RequestToMessageType(), timeStamp: DateTime.UtcNow),
                new MessageBody(JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.General)))));

        [SecondUnwrapWith(0)]
        public Task<EventCommand> MapToRequestAsync(Message message, CancellationToken cancellationToken = default)
            => Task.FromResult(JsonSerializer.Deserialize<EventCommand>(message.Body.Value));
    }
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
