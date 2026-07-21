using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Observability;
using Polly.Registry;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Deposit
{
    public class CommandProcessorBulkDepositPostWithTransactionTests
    {
        private readonly RoutingKey _commandTopic = new("MyCommand");
        private readonly RoutingKey _eventTopic = new("MyEvent");
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new();
        private readonly MyCommand _myCommandTwo = new();
        private readonly MyEvent _myEvent = new();
        private readonly List<Message> _messages = [];
        private readonly SpyOutbox _spyOutbox;
        private readonly SpyTransactionProvider _transactionProvider = new();
        private readonly InternalBus _bus = new();
        public CommandProcessorBulkDepositPostWithTransactionTests()
        {
            _myCommand.Value = "Hello World";
            var timeProvider = new FakeTimeProvider();
            InMemoryMessageProducer commandMessageProducer = new(_bus, new Publication { Topic = new RoutingKey(_commandTopic), RequestType = typeof(MyCommand) });
            InMemoryMessageProducer eventMessageProducer = new(_bus, new Publication { Topic = new RoutingKey(_eventTopic), RequestType = typeof(MyEvent) });
            _messages.Add(new Message(new MessageHeader(_myCommand.Id, _commandTopic, MessageType.MT_COMMAND), new MessageBody(JsonSerializer.Serialize(_myCommand, JsonSerialisationOptions.Options))));
            _messages.Add(new Message(new MessageHeader(_myCommandTwo.Id, _commandTopic, MessageType.MT_COMMAND), new MessageBody(JsonSerializer.Serialize(_myCommandTwo, JsonSerialisationOptions.Options))));
            _messages.Add(new Message(new MessageHeader(_myEvent.Id, _eventTopic, MessageType.MT_EVENT), new MessageBody(JsonSerializer.Serialize(_myEvent, JsonSerialisationOptions.Options))));
            var messageMapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory((type) =>
            {
                if (type == typeof(MyCommandMessageMapper))
                    return new MyCommandMessageMapper();
                else if (type == typeof(MyEventMessageMapper))
                    return new MyEventMessageMapper();
                throw new ConfigurationException($"No command or event mappers registered for {type.Name}");
            }), null);
            messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();
            messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();
            var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer> { { _commandTopic, commandMessageProducer }, { _eventTopic, eventMessageProducer } });
            var resiliencePipelineRegistry = new ResiliencePipelineRegistry<string>().AddBrighterDefault();
            var tracer = new BrighterTracer();
            _spyOutbox = new SpyOutbox()
            {
                Tracer = tracer
            };
            IAmAnOutboxProducerMediator bus = new OutboxProducerMediator<Message, SpyTransaction>(producerRegistry, resiliencePipelineRegistry, messageMapperRegistry, new EmptyMessageTransformerFactory(), new EmptyMessageTransformerFactoryAsync(), tracer, new FindPublicationByPublicationTopicOrRequestType(), _spyOutbox);
            var scheduler = new InMemorySchedulerFactory();
            _commandProcessor = new CommandProcessor(new InMemoryRequestContextFactory(), new DefaultPolicy(), resiliencePipelineRegistry, bus, scheduler, typeof(SpyTransaction));
        }

        [Test]
        public async Task When_depositing_messages_in_the_outbox_with_a_transaction()
        {
            //act
            var requests = new List<IRequest>
            {
                _myCommand,
                _myCommandTwo,
                _myEvent
            };
            _commandProcessor.DepositPost(requests, _transactionProvider);
            //assert
            //messages should not be in the outbox
            await Assert.That((_spyOutbox.Messages).Any(m => m.Message.Id == _myCommand.Id)).IsFalse();
            await Assert.That((_spyOutbox.Messages).Any(m => m.Message.Id == _myCommandTwo.Id)).IsFalse();
            await Assert.That((_spyOutbox.Messages).Any(m => m.Message.Id == _myEvent.Id)).IsFalse();
            //messages should be in the current transaction
            var transaction = await _transactionProvider.GetTransactionAsync();
            List<Message?> messages = requests.Select(r => transaction.Get(r.Id)).ToList();
            await Assert.That((messages).Any(m => m is null)).IsFalse();
            //messages should not be posted
            await Assert.That(_bus.Stream(new RoutingKey(_commandTopic)).Any()).IsFalse();
            await Assert.That(_bus.Stream(new RoutingKey(_eventTopic)).Any()).IsFalse();
            //messages should correspond to the command
            for (var i = 0; i < messages.Count; i++)
            {
                await Assert.That(messages[i]?.Id).IsEqualTo(_messages[i].Id);
                await Assert.That(messages[i]?.Body.Value).IsEqualTo(_messages[i].Body.Value);
                await Assert.That(messages[i]?.Header.Topic).IsEqualTo(_messages[i].Header.Topic);
                await Assert.That(messages[i]?.Header.MessageType).IsEqualTo(_messages[i].Header.MessageType);
            }
        }
    }
}
