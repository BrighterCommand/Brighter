using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Observability;
using Polly.Registry;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Deposit
{
    public class CommandProcessorDepositPostTestsAsync
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
            InMemoryMessageProducer messageProducer = new(_internalBus, new Publication { Topic = _routingKey, RequestType = typeof(MyCommand) });
            _message = new Message(new MessageHeader(_myCommand.Id, _routingKey, MessageType.MT_COMMAND), new MessageBody(JsonSerializer.Serialize(_myCommand, JsonSerialisationOptions.Options)));
            var messageMapperRegistry = new MessageMapperRegistry(null, new SimpleMessageMapperFactoryAsync((_) => new MyCommandMessageMapperAsync()));
            messageMapperRegistry.RegisterAsync<MyCommand, MyCommandMessageMapperAsync>();
            var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer> { { _routingKey, messageProducer }, });
            var resiliencePipelineRegistry = new ResiliencePipelineRegistry<string>().AddBrighterDefault();
            var tracer = new BrighterTracer();
            _outbox = new InMemoryOutbox(timeProvider)
            {
                Tracer = tracer
            };
            IAmAnOutboxProducerMediator bus = new OutboxProducerMediator<Message, CommittableTransaction>(producerRegistry, resiliencePipelineRegistry, messageMapperRegistry, new EmptyMessageTransformerFactory(), new EmptyMessageTransformerFactoryAsync(), tracer, new FindPublicationByPublicationTopicOrRequestType(), _outbox);
            _commandProcessor = new CommandProcessor(new InMemoryRequestContextFactory(), new DefaultPolicy(), resiliencePipelineRegistry, bus, new InMemorySchedulerFactory());
        }

        [Test]
        public async Task When_depositing_a_message_in_the_outbox_async()
        {
            //act
            await _commandProcessor.DepositPostAsync(_myCommand);
            var context = new RequestContext();
            //assert
            //message should not be posted
            await Assert.That(_internalBus.Stream(_routingKey).Any()).IsFalse();
            //message should be in the store
            var depositedPost = (await _outbox.OutstandingMessagesAsync(TimeSpan.Zero, context)).SingleOrDefault(msg => msg.Id == _message.Id);
            await Assert.That(depositedPost).IsNotNull();
            //message should correspond to the command
            await Assert.That(depositedPost.Id).IsEqualTo(_message.Id);
            await Assert.That(depositedPost.Body.Value).IsEqualTo(_message.Body.Value);
            await Assert.That(depositedPost.Header.Topic).IsEqualTo(_message.Header.Topic);
            await Assert.That(depositedPost.Header.MessageType).IsEqualTo(_message.Header.MessageType);
            //message should be marked as outstanding if not sent
            var outstandingMessages = await _outbox.OutstandingMessagesAsync(TimeSpan.Zero, context);
            var outstandingMessage = outstandingMessages.Single();
            await Assert.That(outstandingMessage.Id).IsEqualTo(_message.Id);
        }
    }
}