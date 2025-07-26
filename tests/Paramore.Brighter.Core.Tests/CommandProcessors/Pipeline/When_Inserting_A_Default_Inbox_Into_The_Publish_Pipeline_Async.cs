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
    public class CommandProcessorBuildDefaultInboxPublishAsyncTests : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly InMemoryInbox _inbox = new InMemoryInbox(new FakeTimeProvider());

        public CommandProcessorBuildDefaultInboxPublishAsyncTests()
        {
            var handler = new MyEventHandlerAsync(new Dictionary<string, string>());

            var subscriberRegistry = new SubscriberRegistry();
            //This handler has no Inbox attribute
            subscriberRegistry.RegisterAsync<MyEvent, MyEventHandlerAsync>();

            var container = new ServiceCollection();
            container.AddSingleton(handler);
            container.AddSingleton<IAmAnInboxAsync>(_inbox);
            container.AddTransient<UseInboxHandlerAsync<MyEvent>>();
            container.AddSingleton<IBrighterOptions>(
                new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });

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
            var @event = new MyEvent();
            await _commandProcessor.SendAsync(@event);

            //assert we are in, and auto-context added us under our name
            var boxed = await _inbox.ExistsAsync<MyCommand>(@event.Id, typeof(MyEventHandlerAsync).FullName, null, 100);
            Assert.True(boxed);
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
