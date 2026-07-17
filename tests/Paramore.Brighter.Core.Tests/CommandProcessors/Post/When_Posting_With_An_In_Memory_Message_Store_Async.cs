using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
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
    public class CommandProcessorWithInMemoryOutboxAscyncTests
    {
        private readonly RoutingKey _routingKey = new("MyCommand");
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new MyCommand();
        private readonly Message _message;
        private readonly InMemoryOutbox _outbox;
        private readonly InternalBus _internalBus = new();
        public CommandProcessorWithInMemoryOutboxAscyncTests()
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
            var messageMapperRegistry = new MessageMapperRegistry(null, new SimpleMessageMapperFactoryAsync((_) => new MyCommandMessageMapperAsync()));
            messageMapperRegistry.RegisterAsync<MyCommand, MyCommandMessageMapperAsync>();
            var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer> { { _routingKey, messageProducer }, });
            var resiliencePipelineRegistry = new ResiliencePipelineRegistry<string>().AddBrighterDefault();
            IAmAnOutboxProducerMediator bus = new OutboxProducerMediator<Message, CommittableTransaction>(producerRegistry, resiliencePipelineRegistry, messageMapperRegistry, new EmptyMessageTransformerFactory(), new EmptyMessageTransformerFactoryAsync(), tracer, new FindPublicationByPublicationTopicOrRequestType(), _outbox);
            _commandProcessor = new CommandProcessor(new InMemoryRequestContextFactory(), new DefaultPolicy(), resiliencePipelineRegistry, bus, new InMemorySchedulerFactory());
        }

        [Test]
        public async Task When_Posting_With_An_In_Memory_Outbox_Async()
        {
            await _commandProcessor.PostAsync(_myCommand);
            var message = await _outbox.GetAsync(_myCommand.Id, new RequestContext());
            //Should store the message in the outbox
            await Assert.That(message).IsNotNull();
            //Should send a message via the messaging gateway
            await Assert.That(_internalBus.Stream(new RoutingKey(_routingKey)).Any()).IsTrue();
            //Should convert the command into a message
            await Assert.That(message).IsEqualTo(_message);
        }
    }
}