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
using Polly;
using Polly.Registry;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Post
{
    public class ControlBusSenderPostMessageTests
    {
        private readonly ControlBusSender _controlBusSender;
        private readonly MyCommand _myCommand = new();
        private readonly Message _message;
        private readonly InMemoryOutbox _outbox;
        private readonly FakeTimeProvider _timeProvider;
        public ControlBusSenderPostMessageTests()
        {
            var routingKey = new RoutingKey("MyCommand");
            _myCommand.Value = "Hello World";
            _timeProvider = new FakeTimeProvider();
            InMemoryMessageProducer messageProducer = new(new InternalBus(), new Publication { Topic = routingKey, RequestType = typeof(MyCommand) });
            _message = new Message(new MessageHeader(_myCommand.Id, routingKey, MessageType.MT_COMMAND), new MessageBody(JsonSerializer.Serialize(_myCommand, JsonSerialisationOptions.Options)));
            var messageMapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => new MyCommandMessageMapper()), null);
            messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();
            var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer> { { routingKey, messageProducer }, });
            var resiliencePipelineRegistry = new ResiliencePipelineRegistry<string>().AddBrighterDefault();
            var tracer = new BrighterTracer(_timeProvider);
            _outbox = new InMemoryOutbox(_timeProvider)
            {
                Tracer = tracer
            };
            IAmAnOutboxProducerMediator bus = new OutboxProducerMediator<Message, CommittableTransaction>(producerRegistry, resiliencePipelineRegistry, messageMapperRegistry, new EmptyMessageTransformerFactory(), new EmptyMessageTransformerFactoryAsync(), tracer, new FindPublicationByPublicationTopicOrRequestType(), _outbox, timeProvider: _timeProvider);
            CommandProcessor commandProcessor = new(new InMemoryRequestContextFactory(), new DefaultPolicy(), resiliencePipelineRegistry, bus, new InMemorySchedulerFactory());
            _controlBusSender = new ControlBusSender(commandProcessor);
        }

        [Test]
        public async Task When_Posting_Via_A_Control_Bus_Sender()
        {
            _controlBusSender.Post(_myCommand);
            _timeProvider.Advance(TimeSpan.FromSeconds(30));
            //_should_store_the_message_in_the_sent_command_message_repository
            var message = (await _outbox.DispatchedMessagesAsync(TimeSpan.FromSeconds(10), new RequestContext(), 1)).SingleOrDefault();
            await Assert.That(message).IsNotNull();
            //_should_convert_the_command_into_a_message
            await Assert.That(message).IsEqualTo(_message);
        }

        [After(Test)]
        public void Dispose()
        {
            _controlBusSender.Dispose();
        }
    }
}