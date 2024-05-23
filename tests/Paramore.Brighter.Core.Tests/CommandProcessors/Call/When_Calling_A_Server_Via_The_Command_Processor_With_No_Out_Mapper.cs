using System;
using System.Collections.Generic;
using System.Transactions;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Observability;
using Paramore.Brighter.ServiceActivator.TestHelpers;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Call
{
    [Collection("CommandProcessor")]
    public class CommandProcessorMissingOutMapperTests : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly MyRequest _myRequest = new MyRequest();
        
        public CommandProcessorMissingOutMapperTests()
        {
             _myRequest.RequestValue = "Hello World";

            var messageMapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory((type) =>
            {
                if (type == typeof(MyResponseMessageMapper))
                    return new MyResponseMessageMapper();

                throw new ConfigurationException($"No mapper found for {type.Name}");
            }), null);
            
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

            var policyRegistry = new PolicyRegistry
            {
                { CommandProcessor.RETRYPOLICY, retryPolicy },
                { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy }
            };

            var producerRegistry = new ProducerRegistry(new Dictionary<string, IAmAMessageProducer>
            {
                { "MyRequest", new FakeMessageProducerWithPublishConfirmation() },
            });
            
            IAmAnExternalBusService bus = new ExternalBusService<Message, CommittableTransaction>(
                producerRegistry, 
                policyRegistry,
                messageMapperRegistry,
                new EmptyMessageTransformerFactory(),
                new EmptyMessageTransformerFactoryAsync(),
                new InMemoryOutbox(new BrighterTracer(),new FakeTimeProvider())
            );
        
            CommandProcessor.ClearServiceBus();
            _commandProcessor = new CommandProcessor(
                subscriberRegistry,
                handlerFactory,
                new InMemoryRequestContextFactory(), 
                policyRegistry,
                bus,
                replySubscriptions:replySubs,
                responseChannelFactory: new InMemoryChannelFactory()
            );

            PipelineBuilder<MyResponse>.ClearPipelineCache();
        }
           
        [Fact]
        public void When_Calling_A_Server_Via_The_Command_Processor_With_No_Out_Mapper()
        {
            var exception = Catch.Exception(() => _commandProcessor.Call<MyRequest, MyResponse>(_myRequest, timeOutInMilliseconds: 500));
            
            //should throw an exception as we require a mapper for the outgoing request 
            exception.Should().BeOfType<ConfigurationException>();
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
  }
}
