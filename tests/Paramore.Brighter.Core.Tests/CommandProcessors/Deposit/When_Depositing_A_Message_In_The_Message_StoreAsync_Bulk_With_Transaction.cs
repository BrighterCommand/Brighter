using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Observability;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Deposit
{
    [Collection("CommandProcessor")]
    public class CommandProcessorBulkDepositPostWithTransactionTestsAsync : IDisposable
    {
        private readonly RoutingKey _commandTopic = new("MyCommand");
        private readonly RoutingKey _eventTopic = new("MyEvent");

        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new();
        private readonly MyCommand _myCommandTwo = new();
        private readonly MyEvent _myEvent = new();
        private readonly List<Message> _messages = new List<Message>();
        private readonly SpyOutbox _spyOutbox;
        private readonly SpyTransactionProvider _transactionProvider = new();
        private readonly InternalBus _bus = new();

        public CommandProcessorBulkDepositPostWithTransactionTestsAsync()
        {
            _myCommand.Value = "Hello World";

            var timeProvider = new FakeTimeProvider();
            InMemoryMessageProducer commandMessageProducer = new(_bus, timeProvider, new Publication 
            { 
                Topic = new RoutingKey(_commandTopic), 
                RequestType = typeof(MyCommand) 
            });

            InMemoryMessageProducer eventMessageProducer = new(_bus, timeProvider, new Publication 
            { 
                Topic = new RoutingKey(_eventTopic), 
                RequestType = typeof(MyEvent) 
            });

            _messages.Add(new Message(
                new MessageHeader(_myCommand.Id, _commandTopic, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(_myCommand, JsonSerialisationOptions.Options))
            ));
            _messages.Add(new Message(
                new MessageHeader(_myCommandTwo.Id, _commandTopic, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(_myCommandTwo, JsonSerialisationOptions.Options))
            ));
            _messages.Add(new Message(
                new MessageHeader(_myEvent.Id, _eventTopic, MessageType.MT_EVENT),
                new MessageBody(JsonSerializer.Serialize(_myEvent, JsonSerialisationOptions.Options))
            ));

            var messageMapperRegistry = new MessageMapperRegistry(
                null,
                new SimpleMessageMapperFactoryAsync((type) =>
                {
                    if (type == typeof(MyCommandMessageMapperAsync))
                        return new MyCommandMessageMapperAsync();
                    else
                        return new MyEventMessageMapperAsync();
                })
            );
            messageMapperRegistry.RegisterAsync<MyCommand, MyCommandMessageMapperAsync>();
            messageMapperRegistry.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();

            var retryPolicy = Policy
                .Handle<Exception>()
                .RetryAsync();

            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(1, TimeSpan.FromMilliseconds(1));
            
            var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
            {
                { _commandTopic, commandMessageProducer },
                { _eventTopic, eventMessageProducer}
            });

            var policyRegistry = new PolicyRegistry
            {
                { CommandProcessor.RETRYPOLICYASYNC, retryPolicy },
                { CommandProcessor.CIRCUITBREAKERASYNC, circuitBreakerPolicy }
            };

            var tracer = new BrighterTracer();
            _spyOutbox = new SpyOutbox() {Tracer = tracer};
            
            IAmAnOutboxProducerMediator bus = new OutboxProducerMediator<Message, SpyTransaction>(
                producerRegistry, 
                policyRegistry,
                messageMapperRegistry,
                new EmptyMessageTransformerFactory(),
                new EmptyMessageTransformerFactoryAsync(),
                tracer,
                new FindPublicationByPublicationTopicOrRequestType(),
                _spyOutbox
            );

            CommandProcessor.ClearServiceBus();
            var scheduler = new InMemorySchedulerFactory();
            _commandProcessor = new CommandProcessor(
                new InMemoryRequestContextFactory(),
                policyRegistry,
                bus,
                scheduler,
                _transactionProvider
            );
        }


        [Fact]
        public async Task When_depositing_messages_in_the_outbox_with_a_transaction_async()
        {
            //act
            var requests = new List<IRequest> {_myCommand, _myCommandTwo, _myEvent } ;
            await _commandProcessor.DepositPostAsync(requests);
            var context = new RequestContext();

            //assert

            //messages should not be in the outbox
            Assert.False(_spyOutbox.Messages.Any(m => m.Message.Id == _myCommand.Id));
            Assert.False(_spyOutbox.Messages.Any(m => m.Message.Id == _myCommandTwo.Id));
            Assert.False(_spyOutbox.Messages.Any(m => m.Message.Id == _myEvent.Id));

            //messages should be in the current transaction
            var transaction = _transactionProvider.GetTransaction();
            List<Message?> messages = requests.Select(r => transaction.Get(r.Id)).ToList();
            Assert.False(messages.Any(m => m is null));

            //messages should not be posted
            Assert.False(_bus.Stream(new RoutingKey(_commandTopic)).Any());
            Assert.False(_bus.Stream(new RoutingKey(_eventTopic)).Any());

            //messages should correspond to the command
            for (var i = 0; i < messages.Count; i++)
            {
                Assert.Equal(_messages[i].Id, messages[i]?.Id);
                Assert.Equal(_messages[i].Body.Value, messages[i]?.Body.Value);
                Assert.Equal(_messages[i].Header.Topic, messages[i]?.Header.Topic);
                Assert.Equal(_messages[i].Header.MessageType, messages[i]?.Header.MessageType);
            }
        }
        
        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
