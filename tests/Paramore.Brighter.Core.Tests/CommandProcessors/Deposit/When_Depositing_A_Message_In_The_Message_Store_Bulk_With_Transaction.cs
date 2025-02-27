using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Observability;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Deposit
{
    [Collection("CommandProcessor")]
    public class CommandProcessorBulkDepositPostWithTransactionTests : IDisposable
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

        public CommandProcessorBulkDepositPostWithTransactionTests()
        {
            _myCommand.Value = "Hello World";

            var timeProvider = new FakeTimeProvider();
            InMemoryProducer commandProducer = new(_bus, timeProvider);
            commandProducer.Publication = new Publication 
            { 
                Topic = new RoutingKey(_commandTopic), 
                RequestType = typeof(MyCommand) 
            };

            InMemoryProducer eventProducer = new(_bus, timeProvider);
            eventProducer.Publication = new Publication 
            { 
                Topic = new RoutingKey(_eventTopic), 
                RequestType = typeof(MyEvent) 
            };

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

            var retryPolicy = Policy
                .Handle<Exception>()
                .Retry();

            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreaker(1, TimeSpan.FromMilliseconds(1));
            
            var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
            {
                { _commandTopic, commandProducer },
                { _eventTopic, eventProducer}
            });

            var policyRegistry = new PolicyRegistry
            {
                { CommandProcessor.RETRYPOLICY, retryPolicy },
                { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy }
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
        public void When_depositing_messages_in_the_outbox_with_a_transaction()
        {
            //act
            var requests = new List<IRequest> {_myCommand, _myCommandTwo, _myEvent } ;
            _commandProcessor.DepositPost(requests);
            var context = new RequestContext();

            //assert

            //messages should not be in the outbox
            _spyOutbox.Messages.Any(m => m.Message.Id == _myCommand.Id).Should().BeFalse();
            _spyOutbox.Messages.Any(m => m.Message.Id == _myCommandTwo.Id).Should().BeFalse();
            _spyOutbox.Messages.Any(m => m.Message.Id == _myEvent.Id).Should().BeFalse();

            //messages should be in the current transaction
            var transaction = _transactionProvider.GetTransaction();
            List<Message?> messages = requests.Select(r => transaction.Get(r.Id)).ToList();
            messages.Any(m => m is null).Should().BeFalse();

            //messages should not be posted
            _bus.Stream(new RoutingKey(_commandTopic)).Any().Should().BeFalse();
            _bus.Stream(new RoutingKey(_eventTopic)).Any().Should().BeFalse();

            //messages should correspond to the command
            for (var i = 0; i < messages.Count; i++)
            {
                messages[i]?.Id.Should().Be(_messages[i].Id);
                messages[i]?.Body.Value.Should().Be(_messages[i].Body.Value);
                messages[i]?.Header.Topic.Should().Be(_messages[i].Header.Topic);
                messages[i]?.Header.MessageType.Should().Be(_messages[i].Header.MessageType);
            }
        }
        
        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
