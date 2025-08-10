using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Mime;
using System.Text.Json;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Observability;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Post
{
    [Collection("CommandProcessor")]
    public class CommandProcessorPostCommandAsyncTests : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new();
        private readonly InMemoryOutbox _outbox;
        private readonly InternalBus _internalBus = new();
        private readonly RoutingKey _routingKey;
        private readonly Message _expectedMessage;
        private readonly PartitionKey _partitionKey = new(Id.Random());
        private readonly Id _workflowId = Id.Random();
        private readonly Id _jobId = Id.Random();

        public CommandProcessorPostCommandAsyncTests()
        {
            _myCommand.Value = "Hello World";
            _routingKey = new RoutingKey("MyCommand");

            var timeProvider = new FakeTimeProvider();
            var cloudEventsType = new CloudEventsType("go.paramore.brighter.test");
            
            InMemoryMessageProducer messageProducer = new(_internalBus, timeProvider,
                new Publication()
                {
                    DataSchema = new Uri("https://goparamore.io/schemas/MyCommand.json"),
                    Source = new Uri("https://goparamore.io"),
                    Subject = "MyCommand",
                    Topic = _routingKey,
                    Type = cloudEventsType,
                    ReplyTo = "MyEvent",
                    RequestType = typeof(MyCommand)
                });
            
            _expectedMessage = new Message(
                new MessageHeader(
                    messageId:_myCommand.Id, 
                    topic: _routingKey,
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
                null,
                new SimpleMessageMapperFactoryAsync((_) => new MyCommandMessageMapperAsync())
                );
            messageMapperRegistry.RegisterAsync<MyCommand, MyCommandMessageMapperAsync>();

            var resiliencePipelineRegistry = new ResiliencePipelineRegistry<string>()
                .AddBrighterDefault();
            
            var producerRegistry = new ProducerRegistry(new Dictionary<ProducerKey, IAmAMessageProducer> {{new ProducerKey(_routingKey, cloudEventsType) , messageProducer},});
           
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
        public async Task When_Posting_A_Message_To_The_Command_Processor_Async()
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

            await _commandProcessor.PostAsync(_myCommand, requestContext);
            
            Assert.True(_internalBus.Stream(_routingKey).Any());
            
            var message = await _outbox.GetAsync(_myCommand.Id, requestContext);
            Assert.NotNull(message);
            
            Debug.Assert(_expectedMessage == message);
            Assert.Equal(_expectedMessage, message);
            
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }

    }
}
