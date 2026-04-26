using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Observability;
using Polly;
using Polly.Registry;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Post
{
    public class CommandProcessorWithInMemoryOutboxTests
    {
        private readonly RoutingKey _routingKey = new("MyCommand");
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new MyCommand();
        private readonly Message _message;
        private readonly InMemoryOutbox _outbox;
        private readonly InternalBus _internalBus = new();
        public CommandProcessorWithInMemoryOutboxTests()
        {
            _myCommand.Value = "Hello World";
            var timeProvider = new FakeTimeProvider();
            InMemoryMessageProducer messageProducer = new(_internalBus, new Publication { Topic = new RoutingKey(_routingKey), RequestType = typeof(MyCommand) });
            _message = new Message(new MessageHeader(_myCommand.Id, _routingKey, MessageType.MT_COMMAND), new MessageBody(JsonSerializer.Serialize(_myCommand, JsonSerialisationOptions.Options)));
            var messageMapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory((_) => new MyCommandMessageMapper()), null);
            messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();
            var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer> { { _routingKey, messageProducer }, });
            var resiliencePipelineRegistry = new ResiliencePipelineRegistry<string>().AddBrighterDefault();
            var tracer = new BrighterTracer(timeProvider);
            _outbox = new InMemoryOutbox(timeProvider)
            {
                Tracer = tracer
            };
            IAmAnOutboxProducerMediator bus = new OutboxProducerMediator<Message, CommittableTransaction>(producerRegistry, resiliencePipelineRegistry, messageMapperRegistry, new EmptyMessageTransformerFactory(), new EmptyMessageTransformerFactoryAsync(), tracer, new FindPublicationByPublicationTopicOrRequestType(), _outbox);
            _commandProcessor = new CommandProcessor(new InMemoryRequestContextFactory(), new DefaultPolicy(), resiliencePipelineRegistry, bus, new InMemorySchedulerFactory());
        }

        [Test]
        public async Task When_Posting_With_An_In_Memory_Outbox()
        {
            var context = new RequestContext();
            _commandProcessor.Post(_myCommand, context);
            await Assert.That(await _outbox.GetAsync(_myCommand.Id, context)).IsNotNull();
            await Assert.That(_internalBus.Stream(new RoutingKey(_routingKey))).IsNotEmpty();
            await Assert.That(await _outbox.GetAsync(_myCommand.Id, context)).IsEqualTo(_message);
        }
    }
}