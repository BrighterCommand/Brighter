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
using Polly.Registry;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Clear
{
    public class CommandProcessorPostBoxBulkClearAsyncTests
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly Message _messageOne;
        private readonly Message _messageTwo;
        private readonly InMemoryOutbox _outbox;
        private readonly InternalBus _internalBus = new();
        private readonly IAmAnOutboxProducerMediator _mediator;
        public CommandProcessorPostBoxBulkClearAsyncTests()
        {
            var myCommand = new MyCommand
            {
                Value = "Hello World"
            };
            var myCommand2 = new MyCommand
            {
                Value = "Hello World 2"
            };
            var timeProvider = new FakeTimeProvider();
            var routingKey = new RoutingKey("MyCommand");
            InMemoryMessageProducer messageProducer = new(_internalBus, new Publication { Topic = routingKey, RequestType = typeof(MyCommand) });
            var routingKeyTwo = new RoutingKey("MyCommand2");
            InMemoryMessageProducer messageProducerTwo = new(_internalBus, new Publication { Topic = routingKeyTwo, RequestType = typeof(MyCommand) });
            _messageOne = new Message(new MessageHeader(myCommand.Id, routingKey, MessageType.MT_COMMAND), new MessageBody(JsonSerializer.Serialize(myCommand, JsonSerialisationOptions.Options)));
            _messageTwo = new Message(new MessageHeader(myCommand.Id, routingKeyTwo, MessageType.MT_COMMAND), new MessageBody(JsonSerializer.Serialize(myCommand2, JsonSerialisationOptions.Options)));
            var messageMapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory((_) => new MyCommandMessageMapper()), null);
            messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();
            var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer> { { routingKey, messageProducer }, { routingKeyTwo, messageProducerTwo } });
            var resiliencePipelineRegistry = new ResiliencePipelineRegistry<string>().AddBrighterDefault();
            var tracer = new BrighterTracer();
            _outbox = new InMemoryOutbox(timeProvider)
            {
                Tracer = tracer
            };
            _mediator = new OutboxProducerMediator<Message, CommittableTransaction>(producerRegistry, resiliencePipelineRegistry, messageMapperRegistry, new EmptyMessageTransformerFactory(), new EmptyMessageTransformerFactoryAsync(), tracer, new FindPublicationByPublicationTopicOrRequestType(), _outbox);
            _commandProcessor = new CommandProcessor(new InMemoryRequestContextFactory(), new DefaultPolicy(), resiliencePipelineRegistry, _mediator, requestSchedulerFactory: new InMemorySchedulerFactory());
        }

        [Test, Skip("Erratic due to timing")]
        public async Task When_Clearing_The_PostBox_On_The_Command_Processor_Async()
        {
            var context = new RequestContext();
            await _outbox.AddAsync(_messageOne, context);
            await _outbox.AddAsync(_messageTwo, context);
            await _mediator.ClearOutstandingFromOutboxAsync(2, TimeSpan.FromMilliseconds(1), true, context);
            await Task.Delay(3000);
            //_should_send_a_message_via_the_messaging_gateway
            var routingKeyOne = new RoutingKey(_messageOne.Header.Topic);
            await Assert.That(_internalBus.Stream(routingKeyOne).Any()).IsTrue();
            var sentMessage = _internalBus.Dequeue(routingKeyOne);
            await Assert.That(sentMessage).IsNotNull();
            await Assert.That(sentMessage.Id).IsEqualTo(_messageOne.Id);
            await Assert.That(sentMessage.Header.Topic).IsEqualTo(_messageOne.Header.Topic);
            await Assert.That(sentMessage.Body.Value).IsEqualTo(_messageOne.Body.Value);
            var routingKeyTwo = new RoutingKey(_messageTwo.Header.Topic);
            await Assert.That(_internalBus.Stream(routingKeyOne).Any()).IsTrue();
            var sentMessage2 = _internalBus.Dequeue(routingKeyTwo);
            await Assert.That(sentMessage2).IsNotNull();
            await Assert.That(sentMessage2.Id).IsEqualTo(_messageTwo.Id);
            await Assert.That(sentMessage2.Header.Topic).IsEqualTo(_messageTwo.Header.Topic);
            await Assert.That(sentMessage2.Body.Value).IsEqualTo(_messageTwo.Body.Value);
        }
    }
}