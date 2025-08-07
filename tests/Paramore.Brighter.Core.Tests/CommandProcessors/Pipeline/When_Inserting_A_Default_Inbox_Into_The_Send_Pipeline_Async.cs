using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Inbox.Handlers;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline
{
    [Collection("CommandProcessor")]
     public class CommandProcessorBuildDefaultInboxSendAsyncTests : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly InMemoryInbox _inbox = new(new FakeTimeProvider());

        public CommandProcessorBuildDefaultInboxSendAsyncTests()
        {
             var handler = new MyCommandHandlerAsync(new Dictionary<string, string>());
            
             var subscriberRegistry = new SubscriberRegistry();
             //This handler has no Inbox attribute
             subscriberRegistry.RegisterAsync<MyCommand, MyCommandHandlerAsync>();
             
             var container = new ServiceCollection();
             container.AddSingleton(handler);
             container.AddSingleton<IAmAnInboxAsync>(_inbox);
             container.AddTransient<UseInboxHandlerAsync<MyCommand>>();
             container.AddSingleton<IBrighterOptions>(new BrighterOptions {HandlerLifetime = ServiceLifetime.Transient});

            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());

             var retryPolicy = Policy
                .Handle<Exception>()
                .RetryAsync();

             var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(1, TimeSpan.FromMilliseconds(1));

             var inboxConfiguration = new InboxConfiguration(
                _inbox //throw on duplicates (we should  be the only entry after)
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
                new ResiliencePipelineRegistry<string>(),
                new InMemorySchedulerFactory(),
                inboxConfiguration: inboxConfiguration
                );
        }
        
        [Fact]
        public async Task WhenInsertingADefaultInboxIntoTheSendPipeline()
        {
            //act
            var command = new MyCommand {Value = "Inbox Capture"};
            await _commandProcessor.SendAsync(command);
            
            //assert we are in, and auto-context added us under our name
            var boxed = await _inbox.ExistsAsync<MyCommand>(command.Id, typeof(MyCommandHandlerAsync).FullName, null, 100);
            Assert.True(boxed);
        }
        
        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
 }
}
