using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;
using A = Paramore.Brighter.Core.Tests.MessageSerialisation.TransformTypeKeyed.A;
using B = Paramore.Brighter.Core.Tests.MessageSerialisation.TransformTypeKeyed.B;
using Reuse = Paramore.Brighter.Core.Tests.MessageSerialisation.TransformTypeKeyed.Reuse;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation
{
    public class When_Building_A_Transform_Pipeline_Disambiguates_Mappers_By_Type
    {
        [Fact]
        public void When_two_mappers_share_a_simple_name_each_wrap_pipeline_should_build_with_its_own_transforms_first_built_first()
        {
            // Arrange
            TransformPipelineBuilder.ClearPipelineCache();
            TransformPipelineBuilder builder = CreateCollidingBuilder();

            // Act — build A (FirstTransform) first, warming the cache, then B (SecondTransform)
            builder.BuildWrapPipeline<A.EventCommand>();
            WrapPipeline<B.EventCommand> pipelineB = builder.BuildWrapPipeline<B.EventCommand>();

            // Assert — B's wrap pipeline carries its own transform, never A's
            string trace = Trace(pipelineB).ToString();
            Assert.Contains("SecondTransform", trace);
            Assert.DoesNotContain("FirstTransform", trace);
        }

        [Fact]
        public void When_two_mappers_share_a_simple_name_each_wrap_pipeline_should_build_with_its_own_transforms_opposite_order()
        {
            // Arrange
            TransformPipelineBuilder.ClearPipelineCache();
            TransformPipelineBuilder builder = CreateCollidingBuilder();

            // Act — build B (SecondTransform) first this time, then A (FirstTransform)
            builder.BuildWrapPipeline<B.EventCommand>();
            WrapPipeline<A.EventCommand> pipelineA = builder.BuildWrapPipeline<A.EventCommand>();

            // Assert — A's wrap pipeline carries its own transform, never B's
            string trace = Trace(pipelineA).ToString();
            Assert.Contains("FirstTransform", trace);
            Assert.DoesNotContain("SecondTransform", trace);
        }

        [Fact]
        public void When_two_mappers_share_a_simple_name_each_unwrap_pipeline_should_build_with_its_own_transforms_first_built_first()
        {
            // Arrange
            TransformPipelineBuilder.ClearPipelineCache();
            TransformPipelineBuilder builder = CreateCollidingBuilder();

            // Act — build A (FirstTransform) first, warming the cache, then B (SecondTransform)
            builder.BuildUnwrapPipeline<A.EventCommand>();
            UnwrapPipeline<B.EventCommand> pipelineB = builder.BuildUnwrapPipeline<B.EventCommand>();

            // Assert — B's unwrap pipeline carries its own transform, never A's
            string trace = Trace(pipelineB).ToString();
            Assert.Contains("SecondTransform", trace);
            Assert.DoesNotContain("FirstTransform", trace);
        }

        [Fact]
        public void When_two_mappers_share_a_simple_name_each_unwrap_pipeline_should_build_with_its_own_transforms_opposite_order()
        {
            // Arrange
            TransformPipelineBuilder.ClearPipelineCache();
            TransformPipelineBuilder builder = CreateCollidingBuilder();

            // Act — build B (SecondTransform) first this time, then A (FirstTransform)
            builder.BuildUnwrapPipeline<B.EventCommand>();
            UnwrapPipeline<A.EventCommand> pipelineA = builder.BuildUnwrapPipeline<A.EventCommand>();

            // Assert — A's unwrap pipeline carries its own transform, never B's
            string trace = Trace(pipelineA).ToString();
            Assert.Contains("FirstTransform", trace);
            Assert.DoesNotContain("SecondTransform", trace);
        }

        [Fact]
        public void When_a_single_mapper_is_built_twice_should_leave_one_entry_per_transform_cache_keyed_by_its_runtime_type()
        {
            // Arrange — a mapper/request unique to this fact, so the process-global mementos hold
            // exactly this mapper's entries and the count assertion stays deterministic
            TransformPipelineBuilder.ClearPipelineCache();

            var registry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory(_ => new Reuse.ReuseMapper()),
                null);
            registry.Register<Reuse.ReuseCommand, Reuse.ReuseMapper>();

            var transformerFactory = new SimpleMessageTransformerFactory(_ => new Reuse.ReuseTransform());
            var builder = new TransformPipelineBuilder(registry, transformerFactory);

            // Act — build the same mapper's wrap and unwrap pipelines twice (single-threaded)
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

        private static TransformPipelineBuilder CreateCollidingBuilder()
        {
            var registry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory(t =>
                    t == typeof(A.CollidingMapper)
                        ? new A.CollidingMapper()
                        : (IAmAMessageMapper)new B.CollidingMapper()),
                null);
            registry.Register<A.EventCommand, A.CollidingMapper>();
            registry.Register<B.EventCommand, B.CollidingMapper>();

            var transformerFactory = new SimpleMessageTransformerFactory(t =>
                t == typeof(A.FirstTransform)
                    ? new A.FirstTransform()
                    : (IAmAMessageTransform)new B.SecondTransform());

            return new TransformPipelineBuilder(registry, transformerFactory);
        }

        private static TransformPipelineTracer Trace<TRequest>(WrapPipeline<TRequest> pipeline)
            where TRequest : class, IRequest
        {
            var pipelineTracer = new TransformPipelineTracer();
            pipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }

        private static TransformPipelineTracer Trace<TRequest>(UnwrapPipeline<TRequest> pipeline)
            where TRequest : class, IRequest
        {
            var pipelineTracer = new TransformPipelineTracer();
            pipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }

        private static IReadOnlyCollection<Type> GetMementoKeys(string fieldName)
        {
            FieldInfo? field = typeof(TransformPipelineBuilder).GetField(
                fieldName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(field);

            var cache = (IDictionary)field!.GetValue(null)!;
            return cache.Keys.Cast<Type>().ToList();
        }
    }
}

namespace Paramore.Brighter.Core.Tests.MessageSerialisation.TransformTypeKeyed.A
{
    using System;
    using System.Text.Json;
    using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
    using Paramore.Brighter.Extensions;

    public sealed class EventCommand : Command
    {
        public EventCommand() : base(Guid.NewGuid()) { }
    }

    internal sealed class FirstTransform : Transform
    {
        public override Message Wrap(Message message, Publication publication) => message;

        public override Message Unwrap(Message message) => message;
    }

    internal sealed class FirstWrapWith : WrapWithAttribute
    {
        public FirstWrapWith(int step) : base(step) { }

        public override Type GetHandlerType() => typeof(FirstTransform);
    }

    internal sealed class FirstUnwrapWith : UnwrapWithAttribute
    {
        public FirstUnwrapWith(int step) : base(step) { }

        public override Type GetHandlerType() => typeof(FirstTransform);
    }

    internal sealed class CollidingMapper : IAmAMessageMapper<EventCommand>
    {
        public IRequestContext Context { get; set; }

        [FirstWrapWith(0)]
        public Message MapToMessage(EventCommand request, Publication publication)
        {
            return new Message(
                new MessageHeader(request.Id, publication.Topic, request.RequestToMessageType(), timeStamp: DateTime.UtcNow),
                new MessageBody(JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.General))));
        }

        [FirstUnwrapWith(0)]
        public EventCommand MapToRequest(Message message)
        {
            return JsonSerializer.Deserialize<EventCommand>(message.Body.Value);
        }
    }
}

namespace Paramore.Brighter.Core.Tests.MessageSerialisation.TransformTypeKeyed.B
{
    using System;
    using System.Text.Json;
    using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
    using Paramore.Brighter.Extensions;

    public sealed class EventCommand : Command
    {
        public EventCommand() : base(Guid.NewGuid()) { }
    }

    internal sealed class SecondTransform : Transform
    {
        public override Message Wrap(Message message, Publication publication) => message;

        public override Message Unwrap(Message message) => message;
    }

    internal sealed class SecondWrapWith : WrapWithAttribute
    {
        public SecondWrapWith(int step) : base(step) { }

        public override Type GetHandlerType() => typeof(SecondTransform);
    }

    internal sealed class SecondUnwrapWith : UnwrapWithAttribute
    {
        public SecondUnwrapWith(int step) : base(step) { }

        public override Type GetHandlerType() => typeof(SecondTransform);
    }

    internal sealed class CollidingMapper : IAmAMessageMapper<EventCommand>
    {
        public IRequestContext Context { get; set; }

        [SecondWrapWith(0)]
        public Message MapToMessage(EventCommand request, Publication publication)
        {
            return new Message(
                new MessageHeader(request.Id, publication.Topic, request.RequestToMessageType(), timeStamp: DateTime.UtcNow),
                new MessageBody(JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.General))));
        }

        [SecondUnwrapWith(0)]
        public EventCommand MapToRequest(Message message)
        {
            return JsonSerializer.Deserialize<EventCommand>(message.Body.Value);
        }
    }
}

namespace Paramore.Brighter.Core.Tests.MessageSerialisation.TransformTypeKeyed.Reuse
{
    using System;
    using System.Text.Json;
    using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
    using Paramore.Brighter.Extensions;

    public sealed class ReuseCommand : Command
    {
        public ReuseCommand() : base(Guid.NewGuid()) { }
    }

    internal sealed class ReuseTransform : Transform
    {
        public override Message Wrap(Message message, Publication publication) => message;

        public override Message Unwrap(Message message) => message;
    }

    internal sealed class ReuseWrapWith : WrapWithAttribute
    {
        public ReuseWrapWith(int step) : base(step) { }

        public override Type GetHandlerType() => typeof(ReuseTransform);
    }

    internal sealed class ReuseUnwrapWith : UnwrapWithAttribute
    {
        public ReuseUnwrapWith(int step) : base(step) { }

        public override Type GetHandlerType() => typeof(ReuseTransform);
    }

    internal sealed class ReuseMapper : IAmAMessageMapper<ReuseCommand>
    {
        public IRequestContext Context { get; set; }

        [ReuseWrapWith(0)]
        public Message MapToMessage(ReuseCommand request, Publication publication)
        {
            return new Message(
                new MessageHeader(request.Id, publication.Topic, request.RequestToMessageType(), timeStamp: DateTime.UtcNow),
                new MessageBody(JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.General))));
        }

        [ReuseUnwrapWith(0)]
        public ReuseCommand MapToRequest(Message message)
        {
            return JsonSerializer.Deserialize<ReuseCommand>(message.Body.Value);
        }
    }
}
