using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Observability;
using Polly;
using Polly.Registry;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Deposit
{
    public class CommandProcessorDepositPostWithTransactionTestsAsync
    {
        private readonly RoutingKey _routingKey = new("MyCommand");
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new MyCommand();
        private readonly Message _message;
        private readonly SpyOutbox _spyOutbox;
        private readonly SpyTransactionProvider _transactionProvider = new();
        private readonly InternalBus _internalBus = new();
        public CommandProcessorDepositPostWithTransactionTestsAsync()
        {
            _myCommand.Value = "Hello World";
            InMemoryMessageProducer messageProducer = new(_internalBus, new Publication { Topic = _routingKey, RequestType = typeof(MyCommand) });
            _message = new Message(new MessageHeader(_myCommand.Id, _routingKey, MessageType.MT_COMMAND), new MessageBody(JsonSerializer.Serialize(_myCommand, JsonSerialisationOptions.Options)));
            var messageMapperRegistry = new MessageMapperRegistry(null, new SimpleMessageMapperFactoryAsync((_) => new MyCommandMessageMapperAsync()));
            messageMapperRegistry.RegisterAsync<MyCommand, MyCommandMessageMapperAsync>();
            var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer> { { _routingKey, messageProducer }, });
            var resiliencePipelineRegistry = new ResiliencePipelineRegistry<string>().AddBrighterDefault();
            var tracer = new BrighterTracer();
            _spyOutbox = new SpyOutbox
            {
                Tracer = tracer
            };
            IAmAnOutboxProducerMediator bus = new OutboxProducerMediator<Message, SpyTransaction>(producerRegistry, resiliencePipelineRegistry, messageMapperRegistry, new EmptyMessageTransformerFactory(), new EmptyMessageTransformerFactoryAsync(), tracer, new FindPublicationByPublicationTopicOrRequestType(), _spyOutbox);
            var scheduler = new InMemorySchedulerFactory();
            _commandProcessor = new CommandProcessor(new InMemoryRequestContextFactory(), new DefaultPolicy(), resiliencePipelineRegistry, bus, scheduler, typeof(SpyTransaction));
        }

        [Test]
        public async Task When_depositing_a_message_in_the_outbox_with_a_transaction_async()
        {
            //act
            var postedMessageId = await _commandProcessor.DepositPostAsync(_myCommand, _transactionProvider);
            var context = new RequestContext();
            //assert
            //message should not be in the outbox
            await Assert.That(_spyOutbox.Messages).DoesNotContain(m => m.Message.Id == postedMessageId);
            //message should be in the current transaction
            var transaction = await _transactionProvider.GetTransactionAsync();
            var message = transaction.Get(postedMessageId);
            await Assert.That(message).IsNotNull();
            //message should not be posted
            await Assert.That(_internalBus.Stream(new RoutingKey(_routingKey)).Any()).IsFalse();
            //message should correspond to the command
            await Assert.That(message.Id).IsEqualTo(_message.Id);
            await Assert.That(message.Body.Value).IsEqualTo(_message.Body.Value);
            await Assert.That(message.Header.Topic).IsEqualTo(_message.Header.Topic);
            await Assert.That(message.Header.MessageType).IsEqualTo(_message.Header.MessageType);
        }
    }
}