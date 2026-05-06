using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
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
    public class CommandProcessorPostBoxImplicitClearTests
    {
        private readonly RoutingKey Topic = new("MyCommand");
        private readonly CommandProcessor _commandProcessor;
        private readonly Message _message;
        private readonly Message _message2;
        private readonly InMemoryOutbox _outbox;
        private readonly InternalBus _bus = new();
        private readonly IAmAnOutboxProducerMediator _mediator;
        public CommandProcessorPostBoxImplicitClearTests()
        {
            var myCommand = new MyCommand
            {
                Value = "Hello World"
            };
            var timeProvider = new FakeTimeProvider();
            InMemoryMessageProducer messageProducer = new(_bus, new Publication { Topic = new RoutingKey(Topic), RequestType = typeof(MyCommand) });
            _message = new Message(new MessageHeader(myCommand.Id, Topic, MessageType.MT_COMMAND), new MessageBody(JsonSerializer.Serialize(myCommand, JsonSerialisationOptions.Options)));
            _message2 = new Message(new MessageHeader(Guid.NewGuid().ToString(), Topic, MessageType.MT_COMMAND), new MessageBody(JsonSerializer.Serialize(myCommand, JsonSerialisationOptions.Options)));
            var messageMapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory((_) => new MyCommandMessageMapper()), null);
            messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();
            var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer> { { Topic, messageProducer }, });
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
        public async Task When_Implicit_Clearing_The_PostBox_On_The_Command_Processor()
        {
            var context = new RequestContext();
            await _outbox.AddAsync(_message, context);
            await _outbox.AddAsync(_message2, context);
            await _mediator.ClearOutstandingFromOutboxAsync(1, TimeSpan.Zero, true, context);
            var topic = new RoutingKey(Topic);
            for (var i = 1; i <= 10; i++)
            {
                if (_bus.Stream(topic).Count() == 1)
                    break;
                Thread.Sleep(i * 100);
            }

            //Try again and kick off another background thread
            for (var i = 1; i <= 10; i++)
            {
                if (_bus.Stream(topic).Count() == 2)
                    break;
                Thread.Sleep(i * 100);
                await _mediator.ClearOutstandingFromOutboxAsync(1, TimeSpan.Zero, true, context);
            }

            //_should_send_a_message_via_the_messaging_gateway
            await Assert.That(_bus.Stream(topic).Any()).IsTrue();
            var sentMessage = _bus.Stream(topic).FirstOrDefault(m => m.Id == _message.Id);
            await Assert.That(sentMessage).IsNotNull();
            await Assert.That(sentMessage.Id).IsEqualTo(_message.Id);
            await Assert.That(sentMessage.Header.Topic).IsEqualTo(_message.Header.Topic);
            await Assert.That(sentMessage.Body.Value).IsEqualTo(_message.Body.Value);
            var sentMessage2 = _bus.Stream(topic).FirstOrDefault(m => m.Id == _message2.Id);
            await Assert.That(sentMessage2).IsNotNull();
            await Assert.That(sentMessage2.Id).IsEqualTo(_message2.Id);
            await Assert.That(sentMessage2.Header.Topic).IsEqualTo(_message2.Header.Topic);
            await Assert.That(sentMessage2.Body.Value).IsEqualTo(_message2.Body.Value);
        }
    }
}