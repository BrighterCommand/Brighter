using System;
using FluentAssertions;
using Paramore.Brighter.Inbox;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using Polly;
using Polly.Registry;
using TinyIoC;
using Xunit;

namespace Paramore.Brighter.Tests.CommandProcessors
{
    public class CommandProcessorBuildDefaultInboxSendTests : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly InMemoryInbox _inbox = new InMemoryInbox();

        public CommandProcessorBuildDefaultInboxSendTests()
        {
             var subscriberRegistry = new SubscriberRegistry();
             //This handler has no Inbox attribute
             subscriberRegistry.Add(typeof(MyCommand), typeof(MyCommandHandler));
             
             var container = new TinyIoCContainer();
             var handlerFactory = new TinyIocHandlerFactory(container);

             container.Register<IHandleRequests<MyCommand>, MyCommandHandler>();
             container.Register<IAmAnInbox>(_inbox);
              
             var retryPolicy = Policy
                .Handle<Exception>()
                .Retry();

             var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreaker(1, TimeSpan.FromMilliseconds(1));

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
                new PolicyRegistry { { CommandProcessor.RETRYPOLICY, retryPolicy }, { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy } },
                inboxConfiguration: inboxConfiguration
                );
            
        }
 
        
        [Fact]
        public void WhenInsertingADefaultInboxIntoTheSendPipeline()
        {
            //act
            var command = new MyCommand(){Value = "Inbox Capture"};
            _commandProcessor.Send(command);
            
            //assert we are in, and auto-context added us under our name
            var boxed = _inbox.Exists<MyCommand>(command.Id, typeof(MyCommandHandler).FullName, 100);
            boxed.Should().BeTrue();
        }
        
        public void Dispose()
        {
            _commandProcessor.Dispose();
        }
 }
}
