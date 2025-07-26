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

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Clear
{
    [Trait("Fragile", "CI")]
    [Collection("CommandProcessor")]
    public class CommandProcessorPostBoxBulkClearAsyncTests : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly Message _messageOne;
        private readonly Message _messageTwo;
        private readonly InMemoryOutbox _outbox;
        private readonly InternalBus _internalBus = new();
        private readonly IAmAnOutboxProducerMediator _mediator;

        public CommandProcessorPostBoxBulkClearAsyncTests()
        {
            var myCommand = new MyCommand{ Value = "Hello World"};
            var myCommand2 = new MyCommand { Value = "Hello World 2" };

            var timeProvider = new FakeTimeProvider();

            var routingKey = new RoutingKey("MyCommand");

            InMemoryMessageProducer messageProducer = new(_internalBus, timeProvider, new Publication{Topic = routingKey, RequestType = typeof(MyCommand)});

            var routingKeyTwo = new RoutingKey("MyCommand2"); 
            InMemoryMessageProducer messageProducerTwo = new(_internalBus, timeProvider, new Publication {Topic = routingKeyTwo, RequestType = typeof(MyCommand)});

            _messageOne = new Message(
                new MessageHeader(myCommand.Id, routingKey, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(myCommand, JsonSerialisationOptions.Options))
                );

            _messageTwo = new Message(
                new MessageHeader(myCommand.Id, routingKeyTwo, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(myCommand2, JsonSerialisationOptions.Options))
            );

            var messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory((_) => new MyCommandMessageMapper()),
                null);
            messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();

            var retryPolicy = Policy
                .Handle<Exception>()
                .RetryAsync();

            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(1, TimeSpan.FromMilliseconds(1));

            var policyRegistry = new PolicyRegistry {{CommandProcessor.RETRYPOLICYASYNC, retryPolicy}, {CommandProcessor.CIRCUITBREAKERASYNC, circuitBreakerPolicy}};
            var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
            {
                { routingKey, messageProducer },
                { routingKeyTwo, messageProducerTwo }
            });

            var tracer = new BrighterTracer();
            _outbox = new InMemoryOutbox(timeProvider) {Tracer = tracer};

            _mediator = new OutboxProducerMediator<Message, CommittableTransaction>(
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
                _mediator,
                requestSchedulerFactory: new InMemorySchedulerFactory()
            );
        }


        [Fact(Skip = "Erratic due to timing")]
        public async Task When_Clearing_The_PostBox_On_The_Command_Processor_Async()
        {
            var context = new RequestContext();
            await _outbox.AddAsync(_messageOne, context);
            await _outbox.AddAsync(_messageTwo, context);

            await _mediator.ClearOutstandingFromOutboxAsync(2, TimeSpan.FromMilliseconds(1), true, context);

            await Task.Delay(3000);

            //_should_send_a_message_via_the_messaging_gateway
            var routingKeyOne = new RoutingKey(_messageOne.Header.Topic);
            Assert.True(_internalBus.Stream(routingKeyOne).Any());

            var sentMessage = _internalBus.Dequeue(routingKeyOne);
            Assert.NotNull(sentMessage);
            Assert.Equal(_messageOne.Id, sentMessage.Id);
            Assert.Equal(_messageOne.Header.Topic, sentMessage.Header.Topic);
            Assert.Equal(_messageOne.Body.Value, sentMessage.Body.Value);

            var routingKeyTwo = new RoutingKey(_messageTwo.Header.Topic);
            Assert.True(_internalBus.Stream(routingKeyOne).Any());

            var sentMessage2 = _internalBus.Dequeue(routingKeyTwo);
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
