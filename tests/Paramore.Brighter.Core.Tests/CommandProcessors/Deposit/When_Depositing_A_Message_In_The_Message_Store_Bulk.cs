using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Transactions;
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
    public class CommandProcessorBulkDepositPostTests : IDisposable
    {
        private readonly RoutingKey _commandTopic = new("MyCommand");
        private readonly RoutingKey _eventTopic = new("MyEvent");

        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new();
        private readonly MyCommand _myCommandTwo = new();
        private readonly MyEvent _myEvent = new();
        private readonly Message _message;
        private readonly Message _messageTwo;
        private readonly Message _messageThree;
        private readonly InMemoryOutbox _outbox;
        private readonly InternalBus _bus = new();

        public CommandProcessorBulkDepositPostTests()
        {
            _myCommand.Value = "Hello World";
            _myCommandTwo.Value = "Hello World Two";
            _myEvent.Data = 3;

            var timeProvider = new FakeTimeProvider();
            InMemoryMessageProducer commandMessageProducer = new(_bus, timeProvider, new Publication 
            { 
                Topic = new RoutingKey(_commandTopic), 
                RequestType = typeof(MyCommand) 
            });

            InMemoryMessageProducer eventMessageProducer = new(_bus, timeProvider,  new Publication 
            { 
                Topic = new RoutingKey(_eventTopic), 
                RequestType = typeof(MyEvent) 
            });
            
            _message = new Message(
                new MessageHeader(_myCommand.Id, _commandTopic, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(_myCommand, JsonSerialisationOptions.Options))
                );
            
            _messageTwo = new Message(
                new MessageHeader(_myCommandTwo.Id, _commandTopic, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(_myCommandTwo, JsonSerialisationOptions.Options))
            );
            
            _messageThree = new Message(
                new MessageHeader(_myEvent.Id, _eventTopic, MessageType.MT_EVENT),
                new MessageBody(JsonSerializer.Serialize(_myEvent, JsonSerialisationOptions.Options))
            );

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
                { _commandTopic, commandMessageProducer },
                { _eventTopic, eventMessageProducer}
            });

            var policyRegistry = new PolicyRegistry
            {
                { CommandProcessor.RETRYPOLICY, retryPolicy },
                { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy }
            };

            var tracer = new BrighterTracer();
            _outbox = new InMemoryOutbox(timeProvider) {Tracer = tracer};
            
            IAmAnOutboxProducerMediator bus = new OutboxProducerMediator<Message, CommittableTransaction>(
                producerRegistry, 
                policyRegistry,
                messageMapperRegistry,
                new EmptyMessageTransformerFactory(),
                new EmptyMessageTransformerFactoryAsync(),
                tracer,
                new FindPublicationByPublicationTopicOrRequestType(),
                _outbox
            );

            CommandProcessor.ClearServiceBus();
            _commandProcessor = new CommandProcessor(
                new InMemoryRequestContextFactory(),
                policyRegistry,
                bus,
                new InMemorySchedulerFactory()
            );
        }


        [Fact]
        public void When_depositing_messages_in_the_outbox()
        {
            //act
            var requests = new List<IRequest> {_myCommand, _myCommandTwo, _myEvent } ;
            _commandProcessor.DepositPost(requests);
            var context = new RequestContext();
            
            //assert
            
            //message should not be posted
            Assert.False(_bus.Stream(_commandTopic).Any());
            Assert.False(_bus.Stream(_eventTopic).Any());

            //message should correspond to the command
            var depositedPost = _outbox.Get(_message.Id, context);
            Assert.Equal(_message.Id, depositedPost.Id);
            Assert.Equal(_message.Body.Value, depositedPost.Body.Value);
            Assert.Equal(_message.Header.Topic, depositedPost.Header.Topic);
            Assert.Equal(_message.Header.MessageType, depositedPost.Header.MessageType);

            var depositedPost2 = _outbox.Get(_messageTwo.Id, context);
            Assert.Equal(_messageTwo.Id, depositedPost2.Id);
            Assert.Equal(_messageTwo.Body.Value, depositedPost2.Body.Value);
            Assert.Equal(_messageTwo.Header.Topic, depositedPost2.Header.Topic);
            Assert.Equal(_messageTwo.Header.MessageType, depositedPost2.Header.MessageType);

            var depositedPost3 = _outbox
                .OutstandingMessages(TimeSpan.Zero, context)
                .SingleOrDefault(msg => msg.Id == _messageThree.Id);
            //message should correspond to the command
            Assert.Equal(_messageThree.Id, depositedPost3.Id);
            Assert.Equal(_messageThree.Body.Value, depositedPost3.Body.Value);
            Assert.Equal(_messageThree.Header.Topic, depositedPost3.Header.Topic);
            Assert.Equal(_messageThree.Header.MessageType, depositedPost3.Header.MessageType);

            //message should be marked as outstanding if not sent
            var outstandingMessages = _outbox.OutstandingMessages(TimeSpan.Zero, context);
            Assert.Equal(3, outstandingMessages.Count());
        }
        
        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
