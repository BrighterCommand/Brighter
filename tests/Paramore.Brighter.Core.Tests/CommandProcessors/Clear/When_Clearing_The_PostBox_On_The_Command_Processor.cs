#region Licence
/* The MIT License (MIT)
...
*/

#endregion

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

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Clear
{
    [Collection("CommandProcessor")]
    public class CommandProcessorPostBoxClearTests : IDisposable
    {
        private readonly RoutingKey _routingKey = new("MyCommand");
        private readonly CommandProcessor _commandProcessor;
        private readonly Message _message;
        private readonly InMemoryOutbox _outbox;
        private readonly InternalBus _internalBus = new();

        public CommandProcessorPostBoxClearTests()
        {
            var myCommand = new MyCommand{ Value = "Hello World"};

            var timeProvider = new FakeTimeProvider();
            InMemoryMessageProducer messageProducer = new(_internalBus, timeProvider, new Publication { Topic = _routingKey, RequestType = typeof(MyCommand) });

            _message = new Message(
                new MessageHeader(myCommand.Id, _routingKey, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(myCommand, JsonSerialisationOptions.Options))
                );

            var messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory((_) => new MyCommandMessageMapper()),
                null);
            messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();

            var retryPolicy = Policy
                .Handle<Exception>()
                .Retry();

            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreaker(1, TimeSpan.FromMilliseconds(1));

            var producerRegistry =
                new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
                {
                    { _routingKey, messageProducer },
                });

            var policyRegistry = new PolicyRegistry
            {
                { CommandProcessor.RETRYPOLICY, retryPolicy },
                { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy }
            };

            var tracer = new BrighterTracer(timeProvider);
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
                requestSchedulerFactory: new InMemorySchedulerFactory()
            );
        }

        [Fact]
        public void When_Clearing_The_PostBox_On_The_Command_Processor()
        {
            //arrange
            var context = new RequestContext();
            _outbox.Add(_message, context);

            _commandProcessor.ClearOutbox(new []{_message.Id});

            //_should_send_a_message_via_the_messaging_gateway
            var topic = new RoutingKey(_routingKey);
            Assert.True(_internalBus.Stream(topic).Any());

            var sentMessage = _internalBus.Dequeue(topic);
            Assert.NotNull(sentMessage);
            Assert.Equal(_message.Id, sentMessage.Id);
            Assert.Equal(_message.Header.Topic, sentMessage.Header.Topic);
            Assert.Equal(_message.Body.Value, sentMessage.Body.Value);
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
