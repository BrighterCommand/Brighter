using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Inbox;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using Polly;
using Polly.Registry;
using TinyIoC;
using Xunit;

namespace Paramore.Brighter.Tests.CommandProcessors
{
    //TODO:
    //Publish as opposed to Send
   
    
    public class CommandProcessorBuildDefaultInboxAsyncTests : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new MyCommand(); 
        private readonly InMemoryInbox _inbox = new InMemoryInbox();

        public CommandProcessorBuildDefaultInboxAsyncTests()
        {
             var handler = new MyCommandHandlerAsync(new Dictionary<string, Guid>());
            
             var subscriberRegistry = new SubscriberRegistry();
             //This handler has no Inbox attribute
             subscriberRegistry.RegisterAsync<MyCommand, MyCommandHandlerAsync>();
             
             var container = new TinyIoCContainer();
             var handlerFactory = new TinyIocHandlerFactoryAsync(container);

             container.Register<MyCommandHandlerAsync>(handler);
             container.Register<IAmAnInboxAsync>(_inbox);
              
             var retryPolicy = Policy
                .Handle<Exception>()
                .RetryAsync();

             var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(1, TimeSpan.FromMilliseconds(1));

             var inboxConfiguration = new InboxConfiguration(
                InboxScope.All, //grab all the events
                true, //grab the context from the handler name
                true, //only allow once
                OnceOnlyAction.Throw //throw on duplicates (we should  be the only entry after)
            );

           _commandProcessor = new CommandProcessor(
                subscriberRegistry, 
                handlerFactory, 
                new InMemoryRequestContextFactory(),
                new PolicyRegistry
                {
                    { CommandProcessor.RETRYPOLICYASYNC, retryPolicy }, 
                    { CommandProcessor.CIRCUITBREAKERASYNC, circuitBreakerPolicy }
                },
                inboxConfiguration: inboxConfiguration
                );
            
        }
 
        
        [Fact]
        public async Task WhenInsertingADefaultInboxIntoThePipeline()
        {
            //act
            var command = new MyCommand(){Value = "Inbox Capture"};
            await _commandProcessor.SendAsync(command);
            
            //assert we are in, and auto-context added us under our name
            var boxed = await _inbox.ExistsAsync<MyCommand>(command.Id, typeof(MyCommandHandlerAsync).FullName, 100);
            boxed.Should().BeTrue();
        }
        
        public void Dispose()
        {
            _commandProcessor.Dispose();
        }
 }
}
