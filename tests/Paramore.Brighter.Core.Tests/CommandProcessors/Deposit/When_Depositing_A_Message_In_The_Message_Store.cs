using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Observability;
using Polly.Registry;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Deposit
{
    public class CommandProcessorDepositPostTests
    {
        private readonly RoutingKey _routingKey = new("MyCommand");
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new MyCommand();
        private readonly Message _message;
        private readonly InMemoryOutbox _fakeOutbox;
        private readonly InternalBus _internalBus = new();
        public CommandProcessorDepositPostTests()
        {
            _myCommand.Value = "Hello World";
            var timeProvider = new FakeTimeProvider();
            InMemoryMessageProducer messageProducer = new(_internalBus, new Publication { Topic = _routingKey, RequestType = typeof(MyCommand) });
            _message = new Message(new MessageHeader(_myCommand.Id, _routingKey, MessageType.MT_COMMAND), new MessageBody(JsonSerializer.Serialize(_myCommand, JsonSerialisationOptions.Options)));
            var messageMapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory((_) => new MyCommandMessageMapper()), null);
            messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();
            var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer> { { _routingKey, messageProducer }, });
            var resiliencePipelineRegistry = new ResiliencePipelineRegistry<string>().AddBrighterDefault();
            var tracer = new BrighterTracer();
            _fakeOutbox = new InMemoryOutbox(timeProvider)
            {
                Tracer = tracer
            };
            IAmAnOutboxProducerMediator bus = new OutboxProducerMediator<Message, CommittableTransaction>(producerRegistry, resiliencePipelineRegistry, messageMapperRegistry, new EmptyMessageTransformerFactory(), new EmptyMessageTransformerFactoryAsync(), tracer, new FindPublicationByPublicationTopicOrRequestType(), _fakeOutbox);
            _commandProcessor = new CommandProcessor(new InMemoryRequestContextFactory(), new DefaultPolicy(), resiliencePipelineRegistry, bus, requestSchedulerFactory: new InMemorySchedulerFactory());
        }

        [Test]
        public async Task When_depositing_a_message_in_the_outbox()
        {
            //act
            var postedMessageId = _commandProcessor.DepositPost(_myCommand);
            var context = new RequestContext();
            //assert
            //message should not be posted
            await Assert.That(_internalBus.Stream(new RoutingKey(_routingKey)).Any()).IsFalse();
            //message should correspond to the command
            var depositedPost = await _fakeOutbox.GetAsync(postedMessageId, context);
            await Assert.That(depositedPost.Id).IsEqualTo(_message.Id);
            await Assert.That(depositedPost.Body.Value).IsEqualTo(_message.Body.Value);
            await Assert.That(depositedPost.Header.Topic).IsEqualTo(_message.Header.Topic);
            await Assert.That(depositedPost.Header.MessageType).IsEqualTo(_message.Header.MessageType);
            //message should be marked as outstanding if not sent
            var outstandingMessages = await _fakeOutbox.OutstandingMessagesAsync(TimeSpan.Zero, context);
            var outstandingMessage = outstandingMessages.Single();
            await Assert.That(outstandingMessage.Id).IsEqualTo(_message.Id);
        }
    }
}