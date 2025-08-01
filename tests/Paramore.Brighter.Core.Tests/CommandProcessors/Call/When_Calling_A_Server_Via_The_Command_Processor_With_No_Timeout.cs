using System;
using System.Collections.Generic;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Observability;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Call
{
    [Collection("CommandProcessor")]
    public class CommandProcessorCallTestsNoTimeout : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly MyRequest _myRequest = new();

        public CommandProcessorCallTestsNoTimeout()
        {
            _myRequest.RequestValue = "Hello World";

            var messageMapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory((type) =>
            {
                if (type == typeof(MyRequestMessageMapper))
                    return new MyRequestMessageMapper();
                if (type == typeof(MyResponseMessageMapper))
                    return new MyResponseMessageMapper();

                throw new ConfigurationException($"No mapper found for {type.Name}");
            }), null);
            
            messageMapperRegistry.Register<MyRequest, MyRequestMessageMapper>();
            messageMapperRegistry.Register<MyResponse, MyResponseMessageMapper>();

            var subscriberRegistry = new SubscriberRegistry();
            subscriberRegistry.Register<MyResponse, MyResponseHandler>();
            var handlerFactory = new SimpleHandlerFactorySync(_ => new MyResponseHandler());

            var retryPolicy = Policy
                .Handle<Exception>()
                .Retry();

            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreaker(1, TimeSpan.FromMilliseconds(1));

            var replySubs = new List<Subscription>
            {
                new Subscription<MyResponse>()
            };
            
            var policyRegistry = new PolicyRegistry()
            {
                {CommandProcessor.RETRYPOLICY, retryPolicy},
                {CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy}
            };

            const string topic = "MyRequest";
            var routingKey = new RoutingKey(topic);
            var fakeTimeProvider = new FakeTimeProvider();

            var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
            {
                { 
                    routingKey, new InMemoryMessageProducer(new InternalBus(), fakeTimeProvider,new Publication {Topic = routingKey, RequestType = typeof(MyRequest)})
                }
            });

            var timeProvider = fakeTimeProvider;
            var tracer = new BrighterTracer(timeProvider);
            IAmAnOutboxProducerMediator bus = new OutboxProducerMediator<Message, CommittableTransaction>(
                producerRegistry, 
                policyRegistry, 
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
                policyRegistry,
                bus,
                replySubscriptions:replySubs,
                responseChannelFactory: new InMemoryChannelFactory(new InternalBus(), TimeProvider.System),
                requestSchedulerFactory: new InMemorySchedulerFactory()
            );
           
            PipelineBuilder<MyRequest>.ClearPipelineCache();

        }


        [Fact]
        public void When_Calling_A_Server_Via_The_Command_Processor_With_No_Timeout()
        {
            var exception = Catch.Exception(() => _commandProcessor.Call<MyRequest, MyResponse>(
                _myRequest, timeOut: TimeSpan.FromMilliseconds(0))
            );
            
            //should throw an exception as we require a timeout to be set
            Assert.IsType<InvalidOperationException>(exception);
        }


        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
