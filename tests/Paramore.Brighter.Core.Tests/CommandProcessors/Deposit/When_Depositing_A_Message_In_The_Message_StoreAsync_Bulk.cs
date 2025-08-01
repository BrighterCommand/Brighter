using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
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
    public class CommandProcessorBulkDepositPostTestsAsync: IDisposable
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

            InMemoryMessageProducer commandMessageProducer = new(_internalBus, timeProvider, new Publication 
            {
                Topic =  new RoutingKey(_commandTopic),
                RequestType = typeof(MyCommand)
            } );

            InMemoryMessageProducer eventMessageProducer = new(_internalBus, timeProvider, new Publication
            {
                Topic =  new RoutingKey(_eventTopic),
                RequestType = typeof(MyEvent)
            });

            _message = new Message(
                new MessageHeader(_myCommand.Id, _commandTopic, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(_myCommand, JsonSerialisationOptions.Options))
                );

            _message2 = new Message(
                new MessageHeader(_myCommand2.Id, _commandTopic, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(_myCommand2, JsonSerialisationOptions.Options))
            );

            _message3 = new Message(
                new MessageHeader(_myEvent.Id, _eventTopic, MessageType.MT_EVENT),
                new MessageBody(JsonSerializer.Serialize(_myEvent, JsonSerialisationOptions.Options))
            );

            var messageMapperRegistry = new MessageMapperRegistry(
                null,
            new SimpleMessageMapperFactoryAsync((type) =>
            {
                if (type == typeof(MyCommandMessageMapperAsync))
                    return new MyCommandMessageMapperAsync();
                else                              
                    return new MyEventMessageMapperAsync();
            }));
            messageMapperRegistry.RegisterAsync<MyCommand, MyCommandMessageMapperAsync>();
            messageMapperRegistry.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();

            var retryPolicy = Policy
                .Handle<Exception>()
                .RetryAsync();

            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(1, TimeSpan.FromMilliseconds(1));

            PolicyRegistry policyRegistry = new PolicyRegistry
            {
                { CommandProcessor.RETRYPOLICYASYNC, retryPolicy },
                { CommandProcessor.CIRCUITBREAKERASYNC, circuitBreakerPolicy }
            };

            var producerRegistry =
                new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
                {
                    { _commandTopic, commandMessageProducer },
                    { _eventTopic, eventMessageProducer }
                }); 
            
            var tracer = new BrighterTracer(new FakeTimeProvider());
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
        public async Task When_depositing_messages_in_the_outbox_async()
        {
            //act
            var context = new RequestContext();
            var requests = new List<IRequest> {_myCommand, _myCommand2, _myEvent } ;
            await _commandProcessor.DepositPostAsync(requests);

            //assert
            //message should not be posted
            Assert.False(_internalBus.Stream(new RoutingKey(_commandTopic)).Any());
            Assert.False(_internalBus.Stream(new RoutingKey(_eventTopic)).Any());

            //message should be in the store
            var depositedPost = _outbox
                .OutstandingMessages(TimeSpan.Zero, context)
                .SingleOrDefault(msg => msg.Id == _message.Id);

            //message should be in the store
            var depositedPost2 = _outbox
                .OutstandingMessages(TimeSpan.Zero, context)
                .SingleOrDefault(msg => msg.Id == _message2.Id);

            //message should be in the store
            var depositedPost3 = _outbox
                .OutstandingMessages(TimeSpan.Zero, context)
                .SingleOrDefault(msg => msg.Id == _message3.Id);

            Assert.NotNull(depositedPost);

            //message should correspond to the command
            Assert.Equal(_message.Id, depositedPost.Id);
            Assert.Equal(_message.Body.Value, depositedPost.Body.Value);
            Assert.Equal(_message.Header.Topic, depositedPost.Header.Topic);
            Assert.Equal(_message.Header.MessageType, depositedPost.Header.MessageType);

            //message should correspond to the command
            Assert.Equal(_message2.Id, depositedPost2.Id);
            Assert.Equal(_message2.Body.Value, depositedPost2.Body.Value);
            Assert.Equal(_message2.Header.Topic, depositedPost2.Header.Topic);
            Assert.Equal(_message2.Header.MessageType, depositedPost2.Header.MessageType);

            //message should correspond to the command
            Assert.Equal(_message3.Id, depositedPost3.Id);
            Assert.Equal(_message3.Body.Value, depositedPost3.Body.Value);
            Assert.Equal(_message3.Header.Topic, depositedPost3.Header.Topic);
            Assert.Equal(_message3.Header.MessageType, depositedPost3.Header.MessageType);
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
     }
}
