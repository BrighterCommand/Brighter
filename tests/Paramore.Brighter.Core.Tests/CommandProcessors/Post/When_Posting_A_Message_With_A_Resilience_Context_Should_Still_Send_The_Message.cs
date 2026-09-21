using System.Collections.Generic;
using System.Linq;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Observability;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Post
{
    public class CommandProcessorPostWithResilienceContextTests
    {
        private const string Topic = "MyCommand";
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new();
        private readonly InMemoryOutbox _outbox;
        private readonly InternalBus _internalBus = new();

        public CommandProcessorPostWithResilienceContextTests()
        {
            _myCommand.Value = "Hello World";

            var timeProvider = new FakeTimeProvider();
            var routingKey = new RoutingKey(Topic);
            var cloudEventsType = new CloudEventsType("go.paramore.brighter.test");

            InMemoryMessageProducer messageProducer = new(_internalBus,
                new Publication { Topic = routingKey, Type = cloudEventsType, RequestType = typeof(MyCommand) });

            var messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory((_) => new MyCommandMessageMapper()),
                null);
            messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();

            var resiliencePipelineRegistry = new ResiliencePipelineRegistry<string>()
                .AddBrighterDefault();

            var producerRegistry = new ProducerRegistry(
                new Dictionary<ProducerKey, IAmAMessageProducer> { { new(routingKey, cloudEventsType), messageProducer } });

            var tracer = new BrighterTracer(timeProvider);
            _outbox = new InMemoryOutbox(timeProvider) { Tracer = tracer };

            IAmAnOutboxProducerMediator bus = new OutboxProducerMediator<Message, CommittableTransaction>(
                producerRegistry,
                resiliencePipelineRegistry,
                messageMapperRegistry,
                new EmptyMessageTransformerFactory(),
                new EmptyMessageTransformerFactoryAsync(),
                tracer,
                new FindPublicationByPublicationTopicOrRequestType(),
                _outbox
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
        public void When_Posting_A_Message_With_A_Resilience_Context_Should_Still_Send_The_Message()
        {
            // Arrange
            // The resilience context is the only thing that differs from an ordinary Post; its
            // presence selects the resilience-context branch of the mediator's sync send path.
            var requestContext = new RequestContext
            {
                ResilienceContext = ResilienceContextPool.Shared.Get()
            };

            // Act
            _commandProcessor.Post(_myCommand, requestContext);

            // Assert
            Assert.True(_internalBus.Stream(new RoutingKey(Topic)).Any());
            Assert.NotNull(_outbox.Get(_myCommand.Id, requestContext));
        }
    }
}
