using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.InMemory.Tests.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Observability;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Sweeper
{
    [Collection("CommandProcess")]
    public class SweeperTestsWithCircuitBreaker : IDisposable
    {
        private readonly Message _messageOne;
        private readonly RoutingKey _routingKeyOne;
        private readonly Message _messageTwo;
        private readonly RoutingKey _routingKeyTwo;
        private readonly InMemoryOutbox _outbox;
        private readonly InternalBus _internalBus = new();
        private readonly IAmAnOutboxProducerMediator _mediator;
        private readonly OutboxSweeper _sweeper;
        private readonly IAmACircuitBreaker _circuitBreaker;
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly TimeSpan _timeSinceSent = TimeSpan.FromMilliseconds(6000);

        public SweeperTestsWithCircuitBreaker()
        {
            // message 1
            var myEvent = new MyEvent() { Value = "MyEvent1" };
            _routingKeyOne = new RoutingKey("routingKey1.MyEvent1");
            InMemoryMessageProducer messageProducer = new(_internalBus, _timeProvider, InstrumentationOptions.All)
            {
                Publication = { Topic = _routingKeyOne, RequestType = typeof(MyEvent) }
            };
            _messageOne = new Message(
                new MessageHeader(myEvent.Id, _routingKeyOne, MessageType.MT_EVENT),
                new MessageBody(JsonSerializer.Serialize(myEvent, JsonSerialisationOptions.Options))
            );

            // message 2
            var myEvent2 = new MyEvent() { Value = "MyEvent2" };
            _routingKeyTwo = new RoutingKey("routingKey2.MyEvent2");
            InMemoryMessageProducer messageProducerTwo = new(_internalBus, _timeProvider, InstrumentationOptions.All)
            {
                Publication = { Topic = _routingKeyTwo, RequestType = typeof(MyEvent) }
            };
            _messageTwo = new Message(
                new MessageHeader(myEvent2.Id, _routingKeyTwo, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(myEvent2, JsonSerialisationOptions.Options))
            );

            var messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory((_) => new MyEventMessageMapper()),
                null);
            messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();


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
                { _routingKeyOne, messageProducer },
                { _routingKeyTwo, messageProducerTwo }
            });

            var tracer = new BrighterTracer();

            _outbox = new InMemoryOutbox(_timeProvider) { Tracer = tracer };

            _circuitBreaker = new InMemoryCircuitBreaker(new CircuitBreakerOptions() { CooldownCount = 1 });

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

            _sweeper = new OutboxSweeper(_timeSinceSent, _mediator, new InMemoryRequestContextFactory());
        }


        [Fact]
        public async Task When_outstanding_in_outbox_with_trippedTopic_sweep_clears_them_async()
        {
            // Arrange
            var context = new RequestContext();
            await _outbox.AddAsync(_messageOne, context);
            await _outbox.AddAsync(_messageTwo, context);
            _circuitBreaker.TripTopic(_messageTwo.Header.Topic);
            _timeProvider.Advance(_timeSinceSent); // advance to pick up messages

            await _sweeper.SweepAsync();
            await Task.Delay(1000); //Give the sweep time to run
            Assert.True(_internalBus.Stream(_routingKeyOne).Any());
            Assert.False(_internalBus.Stream(_routingKeyTwo).Any());
            var sentMessage = _internalBus.Dequeue(_routingKeyOne);
            Assert.NotNull(sentMessage);
            Assert.Equal(_messageOne.Id, sentMessage.Id);
            Assert.Equal(_messageOne.Header.Topic, sentMessage.Header.Topic);
            Assert.Equal(_messageOne.Body.Value, sentMessage.Body.Value);

            // Act (clear outbox for the second time)
            await _sweeper.SweepAsync();
            await Task.Delay(1000); //Give the sweep time to run

            // Assert
            var sentMessage2 = _internalBus.Dequeue(_routingKeyTwo, TimeSpan.FromSeconds(1));
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
