using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.ServiceActivator;
using Polly;
using Polly.Registry;
using Polly.Retry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Call
{
    [Collection("CommandProcessor")]
    public class CommandProcessorCallTests : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly MyRequest _myRequest = new();
        //private readonly Message _message;
        private readonly InternalBus _bus = new() ;
        private readonly string _replyAddressCorrelationId = Guid.NewGuid().ToString();
        private readonly MessageMapperRegistry _messageMapperRegistry;
        private readonly RoutingKey _routingKey;

        public CommandProcessorCallTests()
        {

            var timeProvider = new FakeTimeProvider();
            _routingKey = new RoutingKey("MyRequest");
            var messageProducer = new  InMemoryMessageProducer(_bus, timeProvider, new Publication{Topic = _routingKey, RequestType = typeof(MyRequest)});
            
            _messageMapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory((type) =>
            {
                if (type == typeof(MyRequestMessageMapper))
                    return new MyRequestMessageMapper();
                if (type == typeof(MyResponseMessageMapper))
                    return new MyResponseMessageMapper();
               
                throw new ConfigurationException($"No mapper found for {type.Name}");
            }), null);
            _messageMapperRegistry.Register<MyRequest, MyRequestMessageMapper>();
            _messageMapperRegistry.Register<MyResponse, MyResponseMessageMapper>();
            
            var subscriberRegistry = new SubscriberRegistry();
            subscriberRegistry.Register<MyResponse, MyResponseHandler>();
            var handlerFactory = new SimpleHandlerFactorySync(_ => new MyResponseHandler());

            var retryPolicy = Policy
                .Handle<Exception>()
                .Retry();

            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreaker(1, TimeSpan.FromMilliseconds(1));

            var internalBus = new InternalBus();
            InMemoryChannelFactory inMemoryChannelFactory = new(internalBus, TimeProvider.System);
            
            var replySubs = new List<Subscription>
            {
                new Subscription<MyResponse>()
            };

            var policyRegistry = new PolicyRegistry
            {
                { CommandProcessor.RETRYPOLICY, retryPolicy },
                { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy }
            };
            var producerRegistry =
                new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
                {
                    { _routingKey, messageProducer },
                });

            var tracer = new BrighterTracer();
            IAmAnOutboxProducerMediator bus = new OutboxProducerMediator<Message, CommittableTransaction>(
                producerRegistry, 
                policyRegistry,           
                _messageMapperRegistry,
                new EmptyMessageTransformerFactory(),
                new EmptyMessageTransformerFactoryAsync(),
                tracer,
                new FindPublicationByPublicationTopicOrRequestType(),
                new InMemoryOutbox(timeProvider){Tracer = tracer});
        
            CommandProcessor.ClearServiceBus();
            _commandProcessor = new CommandProcessor(
                subscriberRegistry,
                handlerFactory,
                new InMemoryRequestContextFactory(), 
                policyRegistry,
                bus,
                replySubscriptions:replySubs,
                responseChannelFactory: inMemoryChannelFactory,
                requestSchedulerFactory: new InMemorySchedulerFactory()
            );

            PipelineBuilder<MyRequest>.ClearPipelineCache();
            
            _myRequest.RequestValue = "Hello World";
        }

        [Fact]
        public void When_Calling_A_Server_Via_The_Command_Processor()
        {
            //start a message pump on a new thread, to recieve the Call message
            Channel channel = new(
                new("MyChannel"), _routingKey, 
                new InMemoryMessageConsumer(_routingKey, _bus, TimeProvider.System, TimeSpan.FromMilliseconds(1000))
            );
            
            var messagePump = new Reactor(_commandProcessor, (message) => typeof(MyRequest),_messageMapperRegistry, 
                    new EmptyMessageTransformerFactory(), new InMemoryRequestContextFactory(), channel) 
                { Channel = channel, TimeOut = TimeSpan.FromMilliseconds(5000) };

            //RunAsync the pump on a new thread
            Task pump = Task.Factory.StartNew(() => messagePump.Run());
            
            _commandProcessor.Call<MyRequest, MyResponse>(_myRequest, timeOut: TimeSpan.FromMilliseconds(500));
            
            MyResponseHandler.ShouldReceive(new MyResponse(_myRequest.ReplyAddress) {Id = _myRequest.Id});
            
            channel.Stop(_routingKey);

        }
        
        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }

   }
}
