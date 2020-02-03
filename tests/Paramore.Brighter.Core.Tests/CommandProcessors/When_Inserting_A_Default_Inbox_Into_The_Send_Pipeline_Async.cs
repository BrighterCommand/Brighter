using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Inbox;
using Polly;
using Polly.Registry;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;
using Paramore.Brighter.Inbox.Handlers;

namespace Paramore.Brighter.Core.Tests.CommandProcessors
{
    public class CommandProcessorBuildDefaultInboxSendAsyncTests : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly InMemoryInbox _inbox = new InMemoryInbox();

        public CommandProcessorBuildDefaultInboxSendAsyncTests()
        {
             var handler = new MyCommandHandlerAsync(new Dictionary<string, Guid>());
            
             var subscriberRegistry = new SubscriberRegistry();
             //This handler has no Inbox attribute
             subscriberRegistry.RegisterAsync<MyCommand, MyCommandHandlerAsync>();
             
             var container = new ServiceCollection();
             container.AddSingleton<MyCommandHandlerAsync>(handler);
             container.AddSingleton<IAmAnInboxAsync>(_inbox);
             container.AddTransient<UseInboxHandlerAsync<MyCommand>>();

            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());

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
                (IAmAHandlerFactoryAsync)handlerFactory, 
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
