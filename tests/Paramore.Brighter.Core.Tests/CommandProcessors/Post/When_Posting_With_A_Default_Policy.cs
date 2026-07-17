using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Observability;
using Polly.Registry;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Post
{
    public class PostCommandTests
    {
        private readonly RoutingKey _routingKey = new("MyCommand");
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new();
        private readonly Message _message;
        private readonly InMemoryOutbox _outbox;
        private readonly InternalBus _internalBus = new();
        public PostCommandTests()
        {
            _myCommand.Value = "Hello World";
            var timeProvider = new FakeTimeProvider();
            var tracer = new BrighterTracer(timeProvider);
            _outbox = new InMemoryOutbox(timeProvider)
            {
                Tracer = tracer
            };
            InMemoryMessageProducer messageProducer = new(_internalBus, new Publication { Topic = _routingKey, RequestType = typeof(MyCommand) });
            _message = new Message(new MessageHeader(_myCommand.Id, _routingKey, MessageType.MT_COMMAND), new MessageBody(JsonSerializer.Serialize(_myCommand, JsonSerialisationOptions.Options)));
            var messageMapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory((_) => new MyCommandMessageMapper()), null);
            messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();
            var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer> { { _routingKey, messageProducer }, });
            var externalBus = new OutboxProducerMediator<Message, CommittableTransaction>(producerRegistry: producerRegistry, resiliencePipelineRegistry: new ResiliencePipelineRegistry<string>().AddBrighterDefault(), mapperRegistry: messageMapperRegistry, messageTransformerFactory: new EmptyMessageTransformerFactory(), messageTransformerFactoryAsync: new EmptyMessageTransformerFactoryAsync(), tracer: tracer, publicationFinder: new FindPublicationByPublicationTopicOrRequestType(), outboxCircuitBreaker: new InMemoryOutboxCircuitBreaker(), outbox: _outbox);
            _commandProcessor = CommandProcessorBuilder.StartNew().Handlers(new HandlerConfiguration(new SubscriberRegistry(), new EmptyHandlerFactorySync())).DefaultResilience().ExternalBus(ExternalBusType.FireAndForget, externalBus).ConfigureInstrumentation(new BrighterTracer(TimeProvider.System), InstrumentationOptions.All).RequestContextFactory(new InMemoryRequestContextFactory()).RequestSchedulerFactory(new InMemorySchedulerFactory()).Build();
        }

        [Test]
        public async Task When_Posting_With_A_Default_Policy()
        {
            _commandProcessor.Post(_myCommand);
            await Assert.That(_internalBus.Stream(new RoutingKey(_routingKey)).Any()).IsTrue();
            var message = await _outbox.GetAsync(_myCommand.Id, new RequestContext());
            await Assert.That(message).IsNotNull();
            await Assert.That(message).IsEqualTo(_message);
        }

        internal sealed class EmptyHandlerFactorySync : Paramore.Brighter.IAmAHandlerFactorySync, Paramore.Brighter.IAmAHandlerFactory
        {
            public IHandleRequests Create(Type handlerType, IAmALifetime lifetime)
            {
                return null!;
            }

            public void Release(IHandleRequests handler, IAmALifetime lifetime)
            {
            }
        }
    }
}