using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Inbox;
using Polly;
using Polly.Registry;
using TinyIoC;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors
{
    public class CommandProcessorBuildDefaultInboxPublishAsyncTests : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly InMemoryInbox _inbox = new InMemoryInbox();

        public CommandProcessorBuildDefaultInboxPublishAsyncTests()
        {
             var handler = new MyEventHandlerAsync(new Dictionary<string, Guid>());
            
             var subscriberRegistry = new SubscriberRegistry();
             //This handler has no Inbox attribute
             subscriberRegistry.RegisterAsync<MyEvent, MyEventHandlerAsync>();
             
             var container = new TinyIoCContainer();
             var handlerFactory = new TinyIocHandlerFactoryAsync(container);

             container.Register<MyEventHandlerAsync>(handler);
             container.Register<IAmAnInboxAsync>(_inbox);
              
             var retryPolicy = Policy
                .Handle<Exception>()
                .RetryAsync();

             var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(1, TimeSpan.FromMilliseconds(1));

             var inboxConfiguration = new InboxConfiguration(
                InboxScope.All, //grab all the events
                onceOnly: true, //only allow once
                actionOnExists: OnceOnlyAction.Throw //throw on duplicates (we should  be the only entry after)
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
        public async Task WhenInsertingADefaultInboxIntoTheSendPipeline()
        {
            //act
            var @event = new MyEvent();
            await _commandProcessor.SendAsync(@event);
            
            //assert we are in, and auto-context added us under our name
            var boxed = await _inbox.ExistsAsync<MyCommand>(@event.Id, typeof(MyEventHandlerAsync).FullName, 100);
            boxed.Should().BeTrue();
        }
        
        public void Dispose()
        {
            _commandProcessor.Dispose();
        }
 }
}
