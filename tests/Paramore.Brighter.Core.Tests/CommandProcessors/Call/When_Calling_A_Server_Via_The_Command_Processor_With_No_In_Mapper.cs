using System;
using System.Collections.Generic;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Observability;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Call
{
    [Collection("CommandProcessor")]
    public class CommandProcessorNoInMapperTests : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly MyRequest _myRequest = new MyRequest();

        public CommandProcessorNoInMapperTests()
        {
             _myRequest.RequestValue = "Hello World";

            var messageMapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => new MyRequestMessageMapper()), null);

            messageMapperRegistry.Register<MyRequest, MyRequestMessageMapper>();

            var subscriberRegistry = new SubscriberRegistry();
            subscriberRegistry.Register<MyResponse, MyResponseHandler>();
            var handlerFactory = new SimpleHandlerFactorySync(_ => new MyResponseHandler());

            var timeProvider = new FakeTimeProvider();
            var routingKey = new RoutingKey("MyRequest");

            var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
            {
                { 
                    routingKey, new InMemoryMessageProducer(new InternalBus(), timeProvider, new Publication{Topic = routingKey, RequestType = typeof(MyRequest)})
                 },
            });

            var resiliencePipelineRegistry = new ResiliencePipelineRegistry<string>()
                .AddBrighterDefault();

            var tracer = new BrighterTracer();

            IAmAnOutboxProducerMediator bus = new OutboxProducerMediator<Message, CommittableTransaction>(
                producerRegistry,
                resiliencePipelineRegistry,
                messageMapperRegistry,
                new EmptyMessageTransformerFactory(),
                new EmptyMessageTransformerFactoryAsync(),
                tracer,
                new FindPublicationByPublicationTopicOrRequestType(),
                new InMemoryOutbox( timeProvider) {Tracer = tracer}
                );

            CommandProcessor.ClearServiceBus();
            _commandProcessor = new CommandProcessor(
                subscriberRegistry,
                handlerFactory,
                new InMemoryRequestContextFactory(),
                new DefaultPolicy(),
                resiliencePipelineRegistry,
                bus,
                replySubscriptions:new List<Subscription>(),
                responseChannelFactory: new InMemoryChannelFactory(new InternalBus(), TimeProvider.System),
                requestSchedulerFactory: new InMemorySchedulerFactory()
            );

            PipelineBuilder<MyResponse>.ClearPipelineCache();
        }

        [Fact]
        public void When_Calling_A_Server_Via_The_Command_Processor_With_No_Out_Mapper()
        {
            var exception = Catch.Exception(() => _commandProcessor.Call<MyRequest, MyResponse>(_myRequest, new RequestContext(), TimeSpan.FromMilliseconds(500)));

            //should throw an exception as we require a mapper for the outgoing request
            Assert.IsType<InvalidOperationException>(exception);
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
