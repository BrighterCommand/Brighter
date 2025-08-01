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

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Post
{
    [Trait("Fragile", "CI")]
    [Collection("CommandProcessor")]
     public class ControlBusSenderPostMessageAsyncTests : IDisposable
    {
        private readonly RoutingKey _routingKey = new("MyCommand");
        private readonly ControlBusSender _controlBusSender;
        private readonly MyCommand _myCommand = new();
        private readonly Message _message;
        private readonly IAmAnOutboxSync<Message, CommittableTransaction> _outbox;
        private readonly InternalBus _internalBus = new();

        public ControlBusSenderPostMessageAsyncTests()
        {
            _myCommand.Value = "Hello World";

            var timeProvider = new FakeTimeProvider();
            var tracer = new BrighterTracer(timeProvider);
            _outbox = new InMemoryOutbox(timeProvider) {Tracer = tracer};
            InMemoryMessageProducer messageProducer = new(_internalBus, timeProvider, new Publication { Topic = _routingKey, RequestType = typeof(MyCommand) });

            _message = new Message(
                new MessageHeader(_myCommand.Id, _routingKey, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(_myCommand, JsonSerialisationOptions.Options))
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

            var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer> {{_routingKey, messageProducer},});
            var policyRegistry = new PolicyRegistry { { CommandProcessor.RETRYPOLICYASYNC, retryPolicy }, { CommandProcessor.CIRCUITBREAKERASYNC, circuitBreakerPolicy } };
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
            CommandProcessor commandProcessor = new CommandProcessor(
                new InMemoryRequestContextFactory(),
                policyRegistry,
                bus, 
                new InMemorySchedulerFactory()
            );

            _controlBusSender = new ControlBusSender(commandProcessor);
        }

        [Fact(Skip = "Requires publisher confirmation")]
        public async Task When_Posting_Via_A_Control_Bus_Sender_Async()
        {
            await _controlBusSender.PostAsync(_myCommand);

            //_should_send_a_message_via_the_messaging_gateway
            Assert.True(_internalBus.Stream(new RoutingKey(_routingKey)).Any());
            
            //_should_store_the_message_in_the_sent_command_message_repository
            var message = _outbox
              .DispatchedMessages(TimeSpan.FromMilliseconds(1200000), new RequestContext(), 1)
              .SingleOrDefault();
              
            Assert.NotNull(message);
            
            //_should_convert_the_command_into_a_message
            Assert.Equal(_message, message);
        }

        public void Dispose()
        {
            _controlBusSender.Dispose();
            CommandProcessor.ClearServiceBus();
        }
    }
}
