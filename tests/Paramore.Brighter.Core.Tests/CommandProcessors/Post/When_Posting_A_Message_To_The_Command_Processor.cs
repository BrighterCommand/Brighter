using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Text.Json;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Observability;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Post
{
    [Collection("CommandProcessor")]
    public class CommandProcessorPostCommandTests : IDisposable
    {
        private const string Topic = "MyCommand";
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new();
        private readonly Message _expectedMessage;
        private readonly InMemoryOutbox _outbox;
        private readonly InternalBus _internalBus = new();
        private readonly PartitionKey _partitionKey = new(Uuid.NewAsString());
        private readonly Id _workflowId = Id.Random();
        private readonly Id _jobId = Id.Random();

        public CommandProcessorPostCommandTests()
        {
            _myCommand.Value = "Hello World";
            _myCommand.CorrelationId = Id.Random();

            var timeProvider = new FakeTimeProvider();
            var routingKey = new RoutingKey(Topic);

            var cloudEventsType = new CloudEventsType("go.paramore.brighter.test");
            
            InMemoryMessageProducer messageProducer = new(_internalBus, timeProvider,
                new Publication()
                {
                    DataSchema = new Uri("https://goparamore.io/schemas/MyCommand.json"),
                    Source = new Uri("https://goparamore.io"),
                    Subject = "MyCommand",
                    Topic = routingKey,
                    Type = cloudEventsType,
                    ReplyTo = "MyEvent",
                    RequestType = typeof(MyCommand)
                });

            _expectedMessage = new Message(
                new MessageHeader(
                    messageId:_myCommand.Id, 
                    topic: routingKey,
                    messageType: MessageType.MT_COMMAND,
                    source: messageProducer.Publication.Source,
                    type: messageProducer.Publication.Type,
                    correlationId: _myCommand.CorrelationId,
                    replyTo: messageProducer.Publication.ReplyTo,
                    contentType: new ContentType(MediaTypeNames.Application.Json){CharSet = CharacterEncoding.UTF8.FromCharacterEncoding()},
                    partitionKey: _partitionKey,
                    dataSchema: messageProducer.Publication.DataSchema,
                    subject: messageProducer.Publication.Subject,
                    workflowId: _workflowId,
                    jobId: _jobId
                    ),
                new MessageBody(JsonSerializer.Serialize(_myCommand, JsonSerialisationOptions.Options))
                );

            var messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory((_) => new MyCommandMessageMapper()),
                null);
            messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();

            var resiliencePipelineRegistry = new ResiliencePipelineRegistry<string>()
                .AddBrighterDefault();
            
            var producerRegistry = new ProducerRegistry(new Dictionary<ProducerKey, IAmAMessageProducer> {{new (routingKey, cloudEventsType), messageProducer},});

            var tracer = new BrighterTracer(timeProvider);
            _outbox = new InMemoryOutbox(timeProvider) {Tracer = tracer};
            
            IAmAnOutboxProducerMediator bus = new OutboxProducerMediator<Message, CommittableTransaction>(
                producerRegistry, 
                resiliencePipelineRegistry, 
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
                new DefaultPolicy(),
                resiliencePipelineRegistry,
                bus,
                new InMemorySchedulerFactory()
            );
        }

        [Fact]
        public void When_Posting_A_Message_To_The_Command_Processor()
        {
            var requestContext = new RequestContext
            {
                Bag =
                {
                    [RequestContextBagNames.PartitionKey] = _partitionKey,
                    [RequestContextBagNames.WorkflowId] = _workflowId,
                    [RequestContextBagNames.JobId] = _jobId
                }
            };

            _commandProcessor.Post(_myCommand, requestContext);

            Assert.True(_internalBus.Stream(new RoutingKey(Topic)).Any());

            var message = _outbox.Get(_myCommand.Id, requestContext);
            Assert.NotNull(message);
            
            Assert.Equal(_expectedMessage, message);
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
