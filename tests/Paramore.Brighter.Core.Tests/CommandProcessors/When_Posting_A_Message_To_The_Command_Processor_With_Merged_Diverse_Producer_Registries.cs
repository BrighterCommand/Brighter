using System;
using System.Collections.Generic;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.Extensions;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors;

[Collection("CommandProcessor")]
public class CommandProcessorMergedProducerRegistryTests :IDisposable
{   
    [Fact]
    public void When_Merging_With_An_Empty_Rhs_Registry()
    {
        //arrange
        var topicOne = "MyCommand";
        FakeMessageProducer fakeMessageProducerOne = new FakeMessageProducer();
        
        var producerRegistryOne = new ProducerRegistry(new Dictionary<string, IAmAMessageProducer>
        {
            {topicOne, fakeMessageProducerOne}
        });
        
        
        var topicTwo = "MyEvent";
        FakeMessageProducer fakeMessageProducerTwo = new FakeMessageProducer();
         var producerRegistryTwo = new ProducerRegistry(new Dictionary<string, IAmAMessageProducer>
         {
             {topicTwo, fakeMessageProducerTwo}
         });

         var producerRegistry = producerRegistryOne.Merge(producerRegistryTwo);
         
         var messageMapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory((type) =>
         {
             switch(type)
             {
                 case var _ when type == typeof(MyCommandMessageMapper):
                     return new MyCommandMessageMapper();
                 case var _ when type == typeof(MyEventMessageMapper):
                     return new MyEventMessageMapper();
                 default:
                     throw new ConfigurationException("No command mapper registered for {type}");
             }
         }));
         
         messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();
         messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();
         
         var retryPolicy = Policy
             .Handle<Exception>()
             .Retry();
         
         var circuitBreakerPolicy = Policy
             .Handle<Exception>()
             .CircuitBreaker(1, TimeSpan.FromMilliseconds(1));
         
        var fakeOutbox = new FakeOutboxSync();

        var commandProcessor = new CommandProcessor(
            new InMemoryRequestContextFactory(),
            new PolicyRegistry
            {
                { CommandProcessor.RETRYPOLICY, retryPolicy },
                { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy }
            },
            messageMapperRegistry,
            fakeOutbox,
            producerRegistry);

        //act
        commandProcessor.Post(new MyCommand());
        commandProcessor.Post(new MyEvent());
        
        //assert
        fakeMessageProducerOne.MessageWasSent.Should().BeTrue();
    }
    
    public void Dispose()
    {
        CommandProcessor.ClearExtServiceBus();
    }
}
