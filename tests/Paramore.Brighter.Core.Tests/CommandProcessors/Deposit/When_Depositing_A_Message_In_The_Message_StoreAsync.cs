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
    public class CommandProcessorDepositPostTestsAsync: IDisposable
    {
        private readonly RoutingKey _routingKey = new("MyCommand");

        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new MyCommand();
        private readonly Message _message;
        private readonly InMemoryOutbox _outbox;
        private readonly InternalBus _internalBus = new();

        public CommandProcessorDepositPostTestsAsync()
        {
            _myCommand.Value = "Hello World";

            var timeProvider = new FakeTimeProvider();
            InMemoryMessageProducer messageProducer = new(_internalBus, timeProvider, new Publication{ Topic = _routingKey, RequestType = typeof(MyCommand) });

            _message = new Message(
                new MessageHeader(_myCommand.Id, _routingKey, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(_myCommand, JsonSerialisationOptions.Options))
                );

            var messageMapperRegistry = new MessageMapperRegistry(
                null,
                new SimpleMessageMapperFactoryAsync((_) => new MyCommandMessageMapperAsync())
                );
            messageMapperRegistry.RegisterAsync<MyCommand, MyCommandMessageMapperAsync>();

            var retryPolicy = Policy
                .Handle<Exception>()
                .RetryAsync();

            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(1, TimeSpan.FromMilliseconds(1));

            var policyRegistry = new PolicyRegistry
            {
                { CommandProcessor.RETRYPOLICYASYNC, retryPolicy },
                { CommandProcessor.CIRCUITBREAKERASYNC, circuitBreakerPolicy }
            };

            var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
            {
                { _routingKey, messageProducer },
            });

            var tracer = new BrighterTracer();
            _outbox = new InMemoryOutbox(timeProvider) { Tracer = tracer };

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
        public async Task When_depositing_a_message_in_the_outbox_async()
        {
            //act
            await _commandProcessor.DepositPostAsync(_myCommand);
            var context  = new RequestContext();

            //assert
            //message should not be posted
            Assert.False(_internalBus.Stream(_routingKey).Any());

            //message should be in the store
            var depositedPost = _outbox
                .OutstandingMessages(TimeSpan.Zero, context)
                .SingleOrDefault(msg => msg.Id == _message.Id);

            Assert.NotNull(depositedPost);

            //message should correspond to the command
            Assert.Equal(_message.Id, depositedPost.Id);
            Assert.Equal(_message.Body.Value, depositedPost.Body.Value);
            Assert.Equal(_message.Header.Topic, depositedPost.Header.Topic);
            Assert.Equal(_message.Header.MessageType, depositedPost.Header.MessageType);

            //message should be marked as outstanding if not sent
            var outstandingMessages = await _outbox.OutstandingMessagesAsync(TimeSpan.Zero, context);
            var outstandingMessage = outstandingMessages.Single();
            Assert.Equal(_message.Id, outstandingMessage.Id);
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
     }
}
