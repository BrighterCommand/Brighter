using System;
using System.Collections.Generic;
using System.Transactions;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.ServiceActivator.TestHelpers;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors
{
    [Collection("CommandProcessor")]
    public class CommandProcessorNoInMapperTests : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly MyRequest _myRequest = new MyRequest();

        public CommandProcessorNoInMapperTests()
        {
             _myRequest.RequestValue = "Hello World";

            var messageMapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory((type) =>
            {
                if (type == typeof(MyRequestMessageMapper))
                    return new MyRequestMessageMapper();

                throw new ConfigurationException($"No mapper found for {type.Name}");
            }), null);
            
            messageMapperRegistry.Register<MyRequest, MyRequestMessageMapper>();

            var subscriberRegistry = new SubscriberRegistry();
            subscriberRegistry.Register<MyResponse, MyResponseHandler>();
            var handlerFactory = new TestHandlerFactorySync<MyResponse, MyResponseHandler>(() => new MyResponseHandler());

            var retryPolicy = Policy
                .Handle<Exception>()
                .Retry();

            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreaker(1, TimeSpan.FromMilliseconds(1));

            var replySubscriptions = new List<Subscription>();

            const string topic = "MyRequest";
            var producerRegistry = new ProducerRegistry(new Dictionary<string, IAmAMessageProducer>
            {
                { topic, new FakeMessageProducerWithPublishConfirmation{Publication = {Topic = new RoutingKey(topic), RequestType = typeof(MyRequest)}} },
            });

            var policyRegistry = new PolicyRegistry
            {
                { CommandProcessor.RETRYPOLICY, retryPolicy },
                { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy }
            };
            
            IAmAnExternalBusService bus = new ExternalBusService<Message, CommittableTransaction>(
                producerRegistry, 
                policyRegistry, 
                messageMapperRegistry,
                new EmptyMessageTransformerFactory(),
                new EmptyMessageTransformerFactoryAsync(),
                new InMemoryRequestContextFactory(),
                new InMemoryOutbox()
                );
        
            CommandProcessor.ClearServiceBus();
            _commandProcessor = new CommandProcessor(
                subscriberRegistry,
                handlerFactory,
                new InMemoryRequestContextFactory(), 
                policyRegistry,
                bus,
                replySubscriptions:replySubscriptions,
                responseChannelFactory: new InMemoryChannelFactory()
            );
            
            PipelineBuilder<MyResponse>.ClearPipelineCache();
        } 
        
        [Fact]
        public void When_Calling_A_Server_Via_The_Command_Processor_With_No_Out_Mapper()
        {
            var exception = Catch.Exception(() => _commandProcessor.Call<MyRequest, MyResponse>(_myRequest, 500));
            
            //should throw an exception as we require a mapper for the outgoing request 
            exception.Should().BeOfType<ArgumentOutOfRangeException>();
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
