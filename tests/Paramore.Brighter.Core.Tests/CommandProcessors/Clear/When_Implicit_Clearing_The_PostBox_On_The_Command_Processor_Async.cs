#region Licence
/* The MIT License (MIT)
...
*/

#endregion

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
    [Collection("CommandProcessor")]
    public class CommandProcessorPostBoxImplicitClearAsyncTests : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly Message _message;
        private readonly Message _message2;
        private readonly InMemoryOutbox _outbox;
        private readonly InternalBus _bus = new();
        private readonly RoutingKey _routingKey = new("MyCommand");
        private readonly IAmAnOutboxProducerMediator _mediator;

        public CommandProcessorPostBoxImplicitClearAsyncTests()
        {
            var myCommand = new MyCommand{ Value = "Hello World"};

            var timeProvider = new FakeTimeProvider();

            InMemoryMessageProducer messageProducer = new(_bus, timeProvider, new Publication{Topic = _routingKey, RequestType = typeof(MyCommand)});

            _message = new Message(
                new MessageHeader(myCommand.Id, _routingKey, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(myCommand, JsonSerialisationOptions.Options))
                );

            _message2 = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(myCommand, JsonSerialisationOptions.Options))
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

            var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
            {
                { _routingKey, messageProducer },
            });

            var policyRegistry = new PolicyRegistry
            {
                { CommandProcessor.RETRYPOLICYASYNC, retryPolicy },
                { CommandProcessor.CIRCUITBREAKERASYNC, circuitBreakerPolicy }
            };

            _outbox = new InMemoryOutbox(timeProvider);

            _mediator = new OutboxProducerMediator<Message, CommittableTransaction>(
                producerRegistry,
                policyRegistry,
                messageMapperRegistry,
                new EmptyMessageTransformerFactory(),
                new EmptyMessageTransformerFactoryAsync(),
                new BrighterTracer(timeProvider),
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

        [Fact]
        public async Task When_Implicit_Clearing_The_PostBox_On_The_Command_Processor_Async()
        {
            var context = new RequestContext();
            await _outbox.AddAsync(_message, context);
            await _outbox.AddAsync(_message2, context);

            await _mediator.ClearOutstandingFromOutboxAsync(1,TimeSpan.Zero, true, context);

            for (var i = 1; i <= 10; i++)
            {
                if (_bus.Stream(_routingKey).Count() == 1) break;
                await Task.Delay(i * 100);
            }

            await _mediator.ClearOutstandingFromOutboxAsync(1, TimeSpan.Zero, true, context);

            //Try again and kick off another background thread
            for (var i = 1; i <= 10; i++)
            {
                if (_bus.Stream(_routingKey).Count() == 2)
                    break;
                await Task.Delay(i * 100);
                await _mediator.ClearOutstandingFromOutboxAsync(1, TimeSpan.FromMilliseconds(1), true, context);
            }

            //_should_send_a_message_via_the_messaging_gateway
            var messages = _bus.Stream(_routingKey).ToArray();
            Assert.True(messages.Any());

            var sentMessage = messages.FirstOrDefault(m => m.Id == _message.Id);
            Assert.NotNull(sentMessage);
            Assert.Equal(_message.Id, sentMessage.Id);
            Assert.Equal(_message.Header.Topic, sentMessage.Header.Topic);
            Assert.Equal(_message.Body.Value, sentMessage.Body.Value);

            var sentMessage2 = messages.FirstOrDefault(m => m.Id == _message2.Id);
            Assert.NotNull(sentMessage2);
            Assert.Equal(_message2.Id, sentMessage2.Id);
            Assert.Equal(_message2.Header.Topic, sentMessage2.Header.Topic);
            Assert.Equal(_message2.Body.Value, sentMessage2.Body.Value);
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
