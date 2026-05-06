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

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Deposit
{
    public class CommandProcessorBulkDepositPostTestsAsync
    {
        private readonly RoutingKey _commandTopic = new("MyCommand");
        private readonly RoutingKey _eventTopic = new("MyEvent");
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new();
        private readonly MyCommand _myCommand2 = new();
        private readonly MyEvent _myEvent = new();
        private readonly Message _message;
        private readonly Message _message2;
        private readonly Message _message3;
        private readonly InMemoryOutbox _outbox;
        private readonly InternalBus _internalBus = new();
        public CommandProcessorBulkDepositPostTestsAsync()
        {
            _myCommand.Value = "Hello World";
            _myCommand2.Value = "Update World";
            var timeProvider = new FakeTimeProvider();
            InMemoryMessageProducer commandMessageProducer = new(_internalBus, new Publication { Topic = new RoutingKey(_commandTopic), RequestType = typeof(MyCommand) });
            InMemoryMessageProducer eventMessageProducer = new(_internalBus, new Publication { Topic = new RoutingKey(_eventTopic), RequestType = typeof(MyEvent) });
            _message = new Message(new MessageHeader(_myCommand.Id, _commandTopic, MessageType.MT_COMMAND), new MessageBody(JsonSerializer.Serialize(_myCommand, JsonSerialisationOptions.Options)));
            _message2 = new Message(new MessageHeader(_myCommand2.Id, _commandTopic, MessageType.MT_COMMAND), new MessageBody(JsonSerializer.Serialize(_myCommand2, JsonSerialisationOptions.Options)));
            _message3 = new Message(new MessageHeader(_myEvent.Id, _eventTopic, MessageType.MT_EVENT), new MessageBody(JsonSerializer.Serialize(_myEvent, JsonSerialisationOptions.Options)));
            var messageMapperRegistry = new MessageMapperRegistry(null, new SimpleMessageMapperFactoryAsync((type) =>
            {
                if (type == typeof(MyCommandMessageMapperAsync))
                    return new MyCommandMessageMapperAsync();
                else
                    return new MyEventMessageMapperAsync();
            }));
            messageMapperRegistry.RegisterAsync<MyCommand, MyCommandMessageMapperAsync>();
            messageMapperRegistry.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();
            var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer> { { _commandTopic, commandMessageProducer }, { _eventTopic, eventMessageProducer } });
            var resiliencePipelineRegistry = new ResiliencePipelineRegistry<string>().AddBrighterDefault();
            var tracer = new BrighterTracer(new FakeTimeProvider());
            _outbox = new InMemoryOutbox(timeProvider)
            {
                Tracer = tracer
            };
            IAmAnOutboxProducerMediator bus = new OutboxProducerMediator<Message, CommittableTransaction>(producerRegistry, resiliencePipelineRegistry, messageMapperRegistry, new EmptyMessageTransformerFactory(), new EmptyMessageTransformerFactoryAsync(), tracer, new FindPublicationByPublicationTopicOrRequestType(), _outbox);
            _commandProcessor = new CommandProcessor(new InMemoryRequestContextFactory(), new DefaultPolicy(), resiliencePipelineRegistry, bus, new InMemorySchedulerFactory());
        }

        [Test]
        public async Task When_depositing_messages_in_the_outbox_async()
        {
            //act
            var context = new RequestContext();
            var requests = new List<IRequest>
            {
                _myCommand,
                _myCommand2,
                _myEvent
            };
            await _commandProcessor.DepositPostAsync(requests);
            //assert
            //message should not be posted
            await Assert.That(_internalBus.Stream(new RoutingKey(_commandTopic)).Any()).IsFalse();
            await Assert.That(_internalBus.Stream(new RoutingKey(_eventTopic)).Any()).IsFalse();
            //message should be in the store
            var depositedPost = (await _outbox.OutstandingMessagesAsync(TimeSpan.Zero, context)).SingleOrDefault(msg => msg.Id == _message.Id);
            //message should be in the store
            var depositedPost2 = (await _outbox.OutstandingMessagesAsync(TimeSpan.Zero, context)).SingleOrDefault(msg => msg.Id == _message2.Id);
            //message should be in the store
            var depositedPost3 = (await _outbox.OutstandingMessagesAsync(TimeSpan.Zero, context)).SingleOrDefault(msg => msg.Id == _message3.Id);
            await Assert.That(depositedPost).IsNotNull();
            //message should correspond to the command
            await Assert.That(depositedPost.Id).IsEqualTo(_message.Id);
            await Assert.That(depositedPost.Body.Value).IsEqualTo(_message.Body.Value);
            await Assert.That(depositedPost.Header.Topic).IsEqualTo(_message.Header.Topic);
            await Assert.That(depositedPost.Header.MessageType).IsEqualTo(_message.Header.MessageType);
            //message should correspond to the command
            await Assert.That(depositedPost2).IsNotNull();
            await Assert.That(depositedPost2.Id).IsEqualTo(_message2.Id);
            await Assert.That(depositedPost2.Body.Value).IsEqualTo(_message2.Body.Value);
            await Assert.That(depositedPost2.Header.Topic).IsEqualTo(_message2.Header.Topic);
            await Assert.That(depositedPost2.Header.MessageType).IsEqualTo(_message2.Header.MessageType);
            //message should correspond to the command
            await Assert.That(depositedPost3).IsNotNull();
            await Assert.That(depositedPost3.Id).IsEqualTo(_message3.Id);
            await Assert.That(depositedPost3.Body.Value).IsEqualTo(_message3.Body.Value);
            await Assert.That(depositedPost3.Header.Topic).IsEqualTo(_message3.Header.Topic);
            await Assert.That(depositedPost3.Header.MessageType).IsEqualTo(_message3.Header.MessageType);
        }
    }
}