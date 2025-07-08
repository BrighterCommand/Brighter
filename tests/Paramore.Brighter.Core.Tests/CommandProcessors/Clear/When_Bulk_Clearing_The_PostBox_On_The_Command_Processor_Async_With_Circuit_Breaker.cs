using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Observability;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Clear
{
    [Collection("CommandProcessor")]
    public class CommandProcessorPostBoxBulkClearAsyncWithCircuitBreakerTests : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly Message _messageOne;
        private readonly Message _messageTwo;
        private readonly InMemoryOutbox _outbox;
        private readonly InternalBus _internalBus = new();
        private readonly IAmAnOutboxProducerMediator _mediator;
        private readonly IAmACircuitBreaker _circuitBreaker;

        public CommandProcessorPostBoxBulkClearAsyncWithCircuitBreakerTests()
        {
            var timeProvider = new FakeTimeProvider();

            // message 1
            var myCommand = new MyCommand{ Value = "Hello World"};
            var routingKey = new RoutingKey("MyCommand");
            InMemoryMessageProducer messageProducer = new(_internalBus, timeProvider, InstrumentationOptions.All)
            {
                Publication = { Topic = routingKey, RequestType = typeof(MyCommand) }
            };
            _messageOne = new Message(
                new MessageHeader(myCommand.Id, routingKey, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(myCommand, JsonSerialisationOptions.Options))
            );

            // message 2
            var myCommand2 = new MyCommand { Value = "Hello World 2" };
            var routingKeyTwo = new RoutingKey("MyCommand2");
            InMemoryMessageProducer messageProducerTwo = new(_internalBus, timeProvider, InstrumentationOptions.All)
            {
                Publication = { Topic = routingKeyTwo, RequestType = typeof(MyCommand) }
            };
            _messageTwo = new Message(
                new MessageHeader(myCommand2.Id, routingKeyTwo, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(myCommand2, JsonSerialisationOptions.Options))
            );

            var messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory((_) => new MyCommandMessageMapper()),
                null);
            messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();


            var policyRegistry = new PolicyRegistry
            {
                {
                    CommandProcessor.RETRYPOLICYASYNC, Policy
                        .Handle<Exception>()
                        .RetryAsync()
                },
                {
                    CommandProcessor.CIRCUITBREAKERASYNC, Policy
                        .Handle<Exception>()
                        .CircuitBreakerAsync(1, TimeSpan.FromMilliseconds(1))
                }
            };

            var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
            {
                { routingKey, messageProducer },
                { routingKeyTwo, messageProducerTwo }
            });

            var tracer = new BrighterTracer();

            _outbox = new InMemoryOutbox(timeProvider) {Tracer = tracer};

            _circuitBreaker = new InMemoryCircuitBreaker(new CircuitBreakerOptions(){CooldownCount = 1});

            _mediator = new OutboxProducerMediator<Message, CommittableTransaction>(
                producerRegistry,
                policyRegistry,
                messageMapperRegistry,
                new EmptyMessageTransformerFactory(),
                new EmptyMessageTransformerFactoryAsync(),
                tracer,
                new FindPublicationByPublicationTopicOrRequestType(),
                circuitBreaker: _circuitBreaker,
                _outbox
            );

            CommandProcessor.ClearServiceBus();

            _commandProcessor = new CommandProcessor(
                new InMemoryRequestContextFactory(),
                policyRegistry,
                _mediator,
                requestSchedulerFactory: new InMemorySchedulerFactory()
            );
        }


        [Fact]
        public async Task When_Clearing_The_PostBox_On_The_Command_Processor_Async()
        {
            // Arrange
            var context = new RequestContext();
            _circuitBreaker.TripTopic(_messageTwo.Header.Topic);

            await _outbox.AddAsync(_messageOne, context);
            await _outbox.AddAsync(_messageTwo, context);

            await _mediator.ClearOutstandingFromOutboxAsync(2, TimeSpan.FromMilliseconds(-1), true, context);

            var routingKeyOne = new RoutingKey(_messageOne.Header.Topic);
            Assert.True(_internalBus.Stream(routingKeyOne).Any());
            var sentMessage = _internalBus.Dequeue(routingKeyOne);
            Assert.NotNull(sentMessage);
            Assert.Equal(_messageOne.Id, sentMessage.Id);
            Assert.Equal(_messageOne.Header.Topic, sentMessage.Header.Topic);
            Assert.Equal(_messageOne.Body.Value, sentMessage.Body.Value);

            var routingKeyTwo = new RoutingKey(_messageTwo.Header.Topic);
            Assert.False(_internalBus.Stream(routingKeyOne).Any());
            
            Assert.False(_internalBus.Stream(routingKeyTwo).Any());


            // Act (clear outbox for the second time)
            await _mediator.ClearOutstandingFromOutboxAsync(2, TimeSpan.FromMilliseconds(-1), true, context);


            // Assert
            var sentMessage2 = _internalBus.Dequeue(routingKeyTwo, TimeSpan.FromSeconds(1));
            Assert.NotNull(sentMessage2);
            Assert.Equal(_messageTwo.Id, sentMessage2.Id);
            Assert.Equal(_messageTwo.Header.Topic, sentMessage2.Header.Topic);
            Assert.Equal(_messageTwo.Body.Value, sentMessage2.Body.Value);
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
