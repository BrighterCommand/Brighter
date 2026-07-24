using System;
using System.Collections.Generic;
using System.Threading;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Observability;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Post
{
    public class CommandProcessorPostMapperReleaseTests
    {
        private const string Topic = "MyCommand";
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new() { Value = "Hello World" };
        private readonly ReleaseTrackingMessageMapperFactory _mapperFactory = new();
        private readonly InternalBus _internalBus = new();

        public CommandProcessorPostMapperReleaseTests()
        {
            var timeProvider = new FakeTimeProvider();
            var routingKey = new RoutingKey(Topic);

            InMemoryMessageProducer messageProducer = new(_internalBus,
                new Publication { Topic = routingKey, RequestType = typeof(MyCommand) });

            var messageMapperRegistry = new MessageMapperRegistry(_mapperFactory, null);
            messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();

            var resiliencePipelineRegistry = new ResiliencePipelineRegistry<string>().AddBrighterDefault();

            var producerRegistry = new ProducerRegistry(
                new Dictionary<RoutingKey, IAmAMessageProducer> { { routingKey, messageProducer } });

            var tracer = new BrighterTracer(timeProvider);

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

            _commandProcessor = new CommandProcessor(
                new InMemoryRequestContextFactory(),
                new DefaultPolicy(),
                resiliencePipelineRegistry,
                bus,
                new InMemorySchedulerFactory()
            );
        }

        [Fact]
        public void When_posting_a_message_should_release_every_mapper_it_creates()
        {
            //act
            _commandProcessor.Post(_myCommand);

            //assert — no GC is forced: release must be deterministic, driven by the mediator
            //disposing the pipeline it built, not by the ~TransformPipeline finalizer
            Assert.True(_mapperFactory.CreateCount > 0);
            Assert.Equal(_mapperFactory.CreateCount, _mapperFactory.ReleaseCount);
        }

        // Counts mappers handed out against mappers handed back. A mapper the mediator creates but
        // never returns is one the factory must retain — for an IoC-backed factory, along with the
        // scope it was resolved from — until process shutdown.
        private sealed class ReleaseTrackingMessageMapperFactory : IAmAMessageMapperFactory
        {
            private int _createCount;
            private int _releaseCount;

            public int CreateCount => _createCount;
            public int ReleaseCount => _releaseCount;

            public IAmAMessageMapper Create(Type messageMapperType)
            {
                Interlocked.Increment(ref _createCount);
                return new MyCommandMessageMapper();
            }

            public void Release(IAmAMessageMapper mapper)
            {
                Interlocked.Increment(ref _releaseCount);
            }
        }
    }
}
