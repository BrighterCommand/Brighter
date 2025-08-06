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
    public class CommandProcessorPostMissingMessageTransformerTests : IDisposable
    {
        private readonly MyCommand _myCommand = new MyCommand();
        private readonly InMemoryOutbox _outbox;
        private Exception? _exception;
        private readonly MessageMapperRegistry _messageMapperRegistry;
        private readonly ProducerRegistry _producerRegistry;
        private readonly IAmABrighterTracer _tracer;

        public CommandProcessorPostMissingMessageTransformerTests()
        {
            _myCommand.Value = "Hello World";

            var timeProvider = new FakeTimeProvider();
            _tracer = new BrighterTracer(timeProvider);
            _outbox = new InMemoryOutbox(timeProvider) {Tracer = _tracer};

            var routingKey = new RoutingKey("MyTopic");
            
            _messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory(_ => new MyCommandMessageMapper()),
                null);
            _messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();

            _producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
            {
                { 
                    routingKey, new InMemoryMessageProducer(new InternalBus(), new FakeTimeProvider(), new Publication  {Topic = routingKey, RequestType = typeof(MyCommand) })
                },
            });
         }

        [Fact]
        public void When_Creating_A_Command_Processor_Without_Message_Transformer()
        {                                             
            var resiliencePipelineRegistry = new ResiliencePipelineRegistry<string>()
                .AddBrighterDefault();
            
            var exception = Catch.Exception(() => new OutboxProducerMediator<Message, CommittableTransaction>(
                _producerRegistry, 
                resiliencePipelineRegistry,
                _messageMapperRegistry,
                 null!,
                new EmptyMessageTransformerFactoryAsync(),
                _tracer,
                new FindPublicationByPublicationTopicOrRequestType(),
                _outbox)
            );               

            Assert.IsType<ConfigurationException>(exception); 
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
