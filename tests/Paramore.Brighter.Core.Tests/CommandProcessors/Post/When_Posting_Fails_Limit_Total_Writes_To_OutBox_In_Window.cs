using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Observability;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Post
{
    [Collection("CommandProcessor")]
    public class PostFailureLimitCommandTests : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly InMemoryOutbox _outbox;
        private readonly FakeTimeProvider _timeProvider;

        public PostFailureLimitCommandTests()
        {
            var routingKey = new RoutingKey("MyCommand");
            
            IAmAMessageProducer producer = new FakeErroringMessageProducerSync{Publication = { Topic = routingKey, RequestType = typeof(MyCommand)}};

            var messageMapperRegistry =
                new MessageMapperRegistry(
                    new SimpleMessageMapperFactory(_ => new MyCommandMessageMapper()),
                    null);
            messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();

            _timeProvider = new FakeTimeProvider();
            var tracer = new BrighterTracer();
            _outbox = new InMemoryOutbox(_timeProvider) {Tracer = tracer};

            var producerRegistry =
                new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer> { { routingKey, producer }, }); 
            
            var externalBus = new OutboxProducerMediator<Message, CommittableTransaction>(
                producerRegistry: producerRegistry,
                resiliencePipelineRegistry: new ResiliencePipelineRegistry<string>().AddBrighterDefault(),
                mapperRegistry: messageMapperRegistry,
                messageTransformerFactory: new EmptyMessageTransformerFactory(),
                messageTransformerFactoryAsync: new EmptyMessageTransformerFactoryAsync(),     
                tracer,
                outbox: _outbox,
                maxOutStandingMessages:3,
                maxOutStandingCheckInterval: TimeSpan.FromMilliseconds(250),
                publicationFinder: new FindPublicationByPublicationTopicOrRequestType()
            );  
            
            _commandProcessor = CommandProcessorBuilder.StartNew()
                .Handlers(new HandlerConfiguration(new SubscriberRegistry(), new EmptyHandlerFactorySync()))
                .DefaultResilience()
                .ExternalBus(ExternalBusType.FireAndForget, externalBus)
                .ConfigureInstrumentation(new BrighterTracer(TimeProvider.System), InstrumentationOptions.All)
                .RequestContextFactory(new InMemoryRequestContextFactory())
                .RequestSchedulerFactory(new InMemorySchedulerFactory())
                .Build();
        }

        [Fact]
        public async Task When_Posting_Fails_Limit_Total_Writes_To_OutBox_In_Window()
        {
            var sentList = new List<string>(); 
            bool shouldThrowException = false;
            try
            {
                do
                {
                    var command = new MyCommand{Value = $"Hello World: {sentList.Count + 1}"};
                    _commandProcessor.Post(command);
                    sentList.Add(command.Id);
                    
                    _timeProvider.Advance(TimeSpan.FromMilliseconds(500));

                    //We need to wait for the sweeper thread to check the outstanding in the outbox
                    await Task.Delay(50);

                } while (sentList.Count < 10);
            }
            catch (OutboxLimitReachedException)
            {
                shouldThrowException = true;
            }
            
            //We should error before the end
            Assert.True(shouldThrowException);
            
            //should store the message in the sent outbox
            foreach (var id in sentList)
            {
               Assert.NotNull( await _outbox.GetAsync(id, new RequestContext()));
            }
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }

        internal sealed class EmptyHandlerFactorySync : IAmAHandlerFactorySync
        {
            public IHandleRequests Create(Type handlerType, IAmALifetime lifetime)
            {
                return null!;
            }

            public void Release(IHandleRequests handler, IAmALifetime lifetime) {}
        }
    }
}
