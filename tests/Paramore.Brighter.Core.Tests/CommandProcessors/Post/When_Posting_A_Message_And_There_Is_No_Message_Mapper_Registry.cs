using System;
using System.Collections.Generic;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Observability;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Post
{
    [Collection("CommandProcessor")]
    public class CommandProcessorNoMessageMapperTests : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new();

        public CommandProcessorNoMessageMapperTests()
        {
            var routingKey = new RoutingKey("MyCommand");
            _myCommand.Value = "Hello World";

            var timeProvider = new FakeTimeProvider();
            InMemoryMessageProducer messageProducer =
                new(new InternalBus(), timeProvider, new Publication { Topic = routingKey, RequestType = typeof(MyCommand) });

            var messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory((_) => new MyCommandMessageMapper()),
                null);

            var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
            {
                { routingKey, messageProducer },
            });

            var resiliencePipelineRegistry = new ResiliencePipelineRegistry<string>()
                .AddBrighterDefault();

            var tracer = new BrighterTracer(timeProvider);
            var outbox = new InMemoryOutbox(timeProvider) {Tracer = tracer};

            IAmAnOutboxProducerMediator bus = new OutboxProducerMediator<Message, CommittableTransaction>(
                producerRegistry, 
                resiliencePipelineRegistry,
                messageMapperRegistry,
                new EmptyMessageTransformerFactory(),
                new EmptyMessageTransformerFactoryAsync(),
                tracer,
                new FindPublicationByPublicationTopicOrRequestType(),
                outbox
            );
        
            CommandProcessor.ClearServiceBus();
            _commandProcessor = new CommandProcessor(
                new InMemoryRequestContextFactory(), 
                new DefaultPolicy(),
                resiliencePipelineRegistry,
                bus,
                new InMemorySchedulerFactory()
            ); 
        }

        [Fact]
        public void When_Posting_A_Message_And_There_Is_No_Message_Mapper_Factory()
        {
            var exception = Catch.Exception(() => _commandProcessor.Post(_myCommand));
            Assert.IsType<ArgumentOutOfRangeException>(exception); 
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
