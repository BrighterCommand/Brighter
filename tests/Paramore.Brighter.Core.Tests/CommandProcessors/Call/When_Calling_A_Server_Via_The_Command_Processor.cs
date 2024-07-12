using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Transactions;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Observability;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Call
{
    [Collection("CommandProcessor")]
    public class CommandProcessorCallTests : IDisposable
    {
        private const string Topic = "MyRequest";
        private readonly CommandProcessor _commandProcessor;
        private readonly MyRequest _myRequest = new();
        private readonly Message _message;
        private readonly InternalBus _bus = new() ;

        public CommandProcessorCallTests()
        {
            _myRequest.RequestValue = "Hello World";

            var timeProvider = new FakeTimeProvider();
            InMemoryProducer producer = new(_bus, timeProvider);
            producer.Publication = new Publication{Topic = new RoutingKey(Topic), RequestType = typeof(MyRequest)};

            var header = new MessageHeader(
                messageId: _myRequest.Id, 
                topic: Topic, 
                messageType:MessageType.MT_COMMAND,
                correlationId: _myRequest.ReplyAddress.CorrelationId,
                replyTo: _myRequest.ReplyAddress.Topic);

            var body = new MessageBody(JsonSerializer.Serialize(new MyRequestDTO(_myRequest.Id, _myRequest.RequestValue), JsonSerialisationOptions.Options));
            _message = new Message(header, body);
 
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
                new ProducerRegistry(new Dictionary<string, IAmAMessageProducer>
                {
                    { Topic, producer },
                });

            var tracer = new BrighterTracer();
            IAmAnExternalBusService bus = new ExternalBusService<Message, CommittableTransaction>(
                producerRegistry, 
                policyRegistry,           
                messageMapperRegistry,
                new EmptyMessageTransformerFactory(),
                new EmptyMessageTransformerFactoryAsync(),
                tracer,
                new InMemoryOutbox(timeProvider){Tracer = tracer});
        
            CommandProcessor.ClearServiceBus();
            _commandProcessor = new CommandProcessor(
                subscriberRegistry,
                handlerFactory,
                new InMemoryRequestContextFactory(), 
                policyRegistry,
                bus,
                replySubscriptions:replySubs,
                responseChannelFactory: inMemoryChannelFactory
            );

            PipelineBuilder<MyRequest>.ClearPipelineCache();
  
        }

        [Fact]
        public void When_Calling_A_Server_Via_The_Command_Processor()
        {
            _commandProcessor.Call<MyRequest, MyResponse>(_myRequest, timeOutInMilliseconds: 500);
            
            var message = _bus.Dequeue(new RoutingKey(Topic));

            message.Should().Be(_message);
            
            MyResponseHandler.ShouldReceive(new MyResponse(_myRequest.ReplyAddress) {Id = _myRequest.Id});

        }
        
        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }

   }
}
