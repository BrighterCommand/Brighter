using System;
using A = Paramore.Brighter.Core.Tests.MessageSerialisation.TransformTypeKeyed.A;
using B = Paramore.Brighter.Core.Tests.MessageSerialisation.TransformTypeKeyed.B;
using Reuse = Paramore.Brighter.Core.Tests.MessageSerialisation.TransformTypeKeyed.Reuse;
using Scenarios = Paramore.Brighter.Core.Tests.MessageSerialisation.TransformTypeKeyed.Scenarios;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation
{
    public class When_Building_A_Transform_Pipeline_Disambiguates_Mappers_By_Type
    {
        [Test]
        public async Task When_two_mappers_share_a_simple_name_each_wrap_pipeline_should_build_with_its_own_transforms_first_built_first()
        {
            // Arrange
            TransformPipelineBuilder builder = CreateCollidingBuilder<Scenarios.WrapFirstBuiltFirst>();

            // Act — build A (FirstTransform) first, warming the cache, then B (SecondTransform)
            builder.BuildWrapPipeline<A.EventCommand<Scenarios.WrapFirstBuiltFirst>>();
            WrapPipeline<B.EventCommand<Scenarios.WrapFirstBuiltFirst>> pipelineB =
                builder.BuildWrapPipeline<B.EventCommand<Scenarios.WrapFirstBuiltFirst>>();

            // Assert — B's wrap pipeline carries its own transform, never A's
            string trace = Trace(pipelineB).ToString();
            await Assert.That(trace).Contains("SecondTransform");
            await Assert.That(trace).DoesNotContain("FirstTransform");
        }

        [Test]
        public async Task When_two_mappers_share_a_simple_name_each_wrap_pipeline_should_build_with_its_own_transforms_opposite_order()
        {
            // Arrange
            TransformPipelineBuilder builder = CreateCollidingBuilder<Scenarios.WrapOppositeOrder>();

            // Act — build B (SecondTransform) first this time, then A (FirstTransform)
            builder.BuildWrapPipeline<B.EventCommand<Scenarios.WrapOppositeOrder>>();
            WrapPipeline<A.EventCommand<Scenarios.WrapOppositeOrder>> pipelineA =
                builder.BuildWrapPipeline<A.EventCommand<Scenarios.WrapOppositeOrder>>();

            // Assert — A's wrap pipeline carries its own transform, never B's
            string trace = Trace(pipelineA).ToString();
            await Assert.That(trace).Contains("FirstTransform");
            await Assert.That(trace).DoesNotContain("SecondTransform");
        }

        [Test]
        public async Task When_two_mappers_share_a_simple_name_each_unwrap_pipeline_should_build_with_its_own_transforms_first_built_first()
        {
            // Arrange
            TransformPipelineBuilder builder = CreateCollidingBuilder<Scenarios.UnwrapFirstBuiltFirst>();

            // Act — build A (FirstTransform) first, warming the cache, then B (SecondTransform)
            builder.BuildUnwrapPipeline<A.EventCommand<Scenarios.UnwrapFirstBuiltFirst>>();
            UnwrapPipeline<B.EventCommand<Scenarios.UnwrapFirstBuiltFirst>> pipelineB =
                builder.BuildUnwrapPipeline<B.EventCommand<Scenarios.UnwrapFirstBuiltFirst>>();

            // Assert — B's unwrap pipeline carries its own transform, never A's
            string trace = Trace(pipelineB).ToString();
            await Assert.That(trace).Contains("SecondTransform");
            await Assert.That(trace).DoesNotContain("FirstTransform");
        }

        [Test]
        public async Task When_two_mappers_share_a_simple_name_each_unwrap_pipeline_should_build_with_its_own_transforms_opposite_order()
        {
            // Arrange
            TransformPipelineBuilder builder = CreateCollidingBuilder<Scenarios.UnwrapOppositeOrder>();

            // Act — build B (SecondTransform) first this time, then A (FirstTransform)
            builder.BuildUnwrapPipeline<B.EventCommand<Scenarios.UnwrapOppositeOrder>>();
            UnwrapPipeline<A.EventCommand<Scenarios.UnwrapOppositeOrder>> pipelineA =
                builder.BuildUnwrapPipeline<A.EventCommand<Scenarios.UnwrapOppositeOrder>>();

            // Assert — A's unwrap pipeline carries its own transform, never B's
            string trace = Trace(pipelineA).ToString();
            await Assert.That(trace).Contains("FirstTransform");
            await Assert.That(trace).DoesNotContain("SecondTransform");
        }

        [Test]
        public async Task When_a_single_mapper_is_built_twice_should_produce_the_same_transform_pipelines()
        {
            // Arrange
            var registry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory(_ => new Reuse.ReuseMapper()),
                null);
            registry.Register<Reuse.ReuseCommand, Reuse.ReuseMapper>();

            var transformerFactory = new SimpleMessageTransformerFactory(_ => new Reuse.ReuseTransform());
            var builder = new TransformPipelineBuilder(registry, transformerFactory);

            // Act
            string firstWrap = Trace(builder.BuildWrapPipeline<Reuse.ReuseCommand>()).ToString();
            string secondWrap = Trace(builder.BuildWrapPipeline<Reuse.ReuseCommand>()).ToString();
            string firstUnwrap = Trace(builder.BuildUnwrapPipeline<Reuse.ReuseCommand>()).ToString();
            string secondUnwrap = Trace(builder.BuildUnwrapPipeline<Reuse.ReuseCommand>()).ToString();

            // Assert
            await Assert.That(secondWrap).IsEqualTo(firstWrap);
            await Assert.That(secondUnwrap).IsEqualTo(firstUnwrap);
        }

        private static TransformPipelineBuilder CreateCollidingBuilder<TScenario>()
        {
            var registry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory(t =>
                    t == typeof(A.CollidingMapper<TScenario>)
                        ? new A.CollidingMapper<TScenario>()
                        : (IAmAMessageMapper)new B.CollidingMapper<TScenario>()),
                null);
            registry.Register<A.EventCommand<TScenario>, A.CollidingMapper<TScenario>>();
            registry.Register<B.EventCommand<TScenario>, B.CollidingMapper<TScenario>>();

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

    }
}

namespace Paramore.Brighter.Core.Tests.MessageSerialisation.TransformTypeKeyed.A
{
    using System;
    using System.Text.Json;
    using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
    using Paramore.Brighter.Extensions;

    public sealed class EventCommand<TScenario> : Command
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

    internal sealed class CollidingMapper<TScenario> : IAmAMessageMapper<EventCommand<TScenario>>
    {
        public IRequestContext Context { get; set; }

        [FirstWrapWith(0)]
        public Message MapToMessage(EventCommand<TScenario> request, Publication publication)
        {
            return new Message(
                new MessageHeader(request.Id, publication.Topic, request.RequestToMessageType(), timeStamp: DateTime.UtcNow),
                new MessageBody(JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.General))));
        }

        [FirstUnwrapWith(0)]
        public EventCommand<TScenario> MapToRequest(Message message)
        {
            return JsonSerializer.Deserialize<EventCommand<TScenario>>(message.Body.Value);
        }
    }
}

namespace Paramore.Brighter.Core.Tests.MessageSerialisation.TransformTypeKeyed.B
{
    using System;
    using System.Text.Json;
    using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
    using Paramore.Brighter.Extensions;

    public sealed class EventCommand<TScenario> : Command
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

    internal sealed class CollidingMapper<TScenario> : IAmAMessageMapper<EventCommand<TScenario>>
    {
        public IRequestContext Context { get; set; }

        [SecondWrapWith(0)]
        public Message MapToMessage(EventCommand<TScenario> request, Publication publication)
        {
            return new Message(
                new MessageHeader(request.Id, publication.Topic, request.RequestToMessageType(), timeStamp: DateTime.UtcNow),
                new MessageBody(JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.General))));
        }

        [SecondUnwrapWith(0)]
        public EventCommand<TScenario> MapToRequest(Message message)
        {
            return JsonSerializer.Deserialize<EventCommand<TScenario>>(message.Body.Value);
        }
    }
}

namespace Paramore.Brighter.Core.Tests.MessageSerialisation.TransformTypeKeyed.Scenarios
{
    internal sealed class WrapFirstBuiltFirst { }
    internal sealed class WrapOppositeOrder { }
    internal sealed class UnwrapFirstBuiltFirst { }
    internal sealed class UnwrapOppositeOrder { }
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
