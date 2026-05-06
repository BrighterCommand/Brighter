using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.Workflows.TestDoubles;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Observability;
using Polly;
using Polly.Registry;
using MyCommand = Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles.MyCommand;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Post
{
    public class CommandProcessorPostCommandMultiChannelTopicTests
    {
        private const string Topic = "MyCommand";
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new();
        private readonly MyOtherCommand _myOtherCommand = new();
        private readonly Message _message;
        private readonly Message _messageTwo;
        private readonly InMemoryOutbox _outbox;
        private readonly InternalBus _internalBus = new();
        public CommandProcessorPostCommandMultiChannelTopicTests()
        {
            _myCommand.Value = "Hello World";
            var timeProvider = new FakeTimeProvider();
            var routingKey = new RoutingKey(Topic);
            var cloudEventsType = new CloudEventsType("io.goparamore.brighter.mycommand");
            var otherEventsType = new CloudEventsType("io.goparamore.brighter.myothercommand");
            var messageProducer = new InMemoryMessageProducer(_internalBus, new Publication { Topic = routingKey, Type = cloudEventsType, RequestType = typeof(MyCommand) });
            //This producer is for a different command type, but the same topic
            var otherMessageProducer = new InMemoryMessageProducer(_internalBus, new Publication { Topic = routingKey, Type = otherEventsType, RequestType = typeof(MyOtherCommand) });
            _message = new Message(new MessageHeader(_myCommand.Id, routingKey, MessageType.MT_COMMAND, type: cloudEventsType), new MessageBody(JsonSerializer.Serialize(_myCommand, JsonSerialisationOptions.Options)));
            _messageTwo = new Message(new MessageHeader(_myOtherCommand.Id, routingKey, MessageType.MT_COMMAND, type: otherEventsType), new MessageBody(JsonSerializer.Serialize(_myOtherCommand, JsonSerialisationOptions.Options)));
            var messageMapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory((mapperType) => mapperType switch
            {
                var t when mapperType == typeof(MyCommandMessageMapper) => new MyCommandMessageMapper(),
                var t when mapperType == typeof(MyOtherCommandMessageMapper) => new MyOtherCommandMessageMapper()}), null);
            messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();
            messageMapperRegistry.Register<MyOtherCommand, MyOtherCommandMessageMapper>();
            var resiliencePipeline = new ResiliencePipelineRegistry<string>().AddBrighterDefault();
            var messageProducers = new Dictionary<ProducerKey, IAmAMessageProducer>
            {
                {
                    new ProducerKey(routingKey, cloudEventsType),
                    messageProducer
                },
                {
                    new ProducerKey(routingKey, otherEventsType),
                    otherMessageProducer
                }
            };
            var producerRegistry = new ProducerRegistry(messageProducers);
            var tracer = new BrighterTracer(timeProvider);
            _outbox = new InMemoryOutbox(timeProvider)
            {
                Tracer = tracer
            };
            IAmAnOutboxProducerMediator bus = new OutboxProducerMediator<Message, CommittableTransaction>(producerRegistry, resiliencePipeline, messageMapperRegistry, new EmptyMessageTransformerFactory(), new EmptyMessageTransformerFactoryAsync(), tracer, new FindPublicationByPublicationTopicOrRequestType(), _outbox);
            _commandProcessor = new CommandProcessor(new InMemoryRequestContextFactory(), new DefaultPolicy(), resiliencePipeline, bus, new InMemorySchedulerFactory());
        }

        [Test]
        public async Task When_Posting_Dynamic_Messages_To_The_Command_Processor()
        {
            _commandProcessor.Post(_myCommand);
            _commandProcessor.Post(_myOtherCommand);
            await Assert.That(_internalBus.Stream(new RoutingKey(Topic)).Any()).IsTrue();
            var message = await _outbox.GetAsync(_myCommand.Id, new RequestContext());
            await Assert.That(message).IsNotNull();
            var otherMessage = await _outbox.GetAsync(_myOtherCommand.Id, new RequestContext());
            await Assert.That(otherMessage).IsNotNull();
            await Assert.That(message).IsEqualTo(_message);
            await Assert.That(otherMessage).IsEqualTo(_messageTwo);
        }
    }
}