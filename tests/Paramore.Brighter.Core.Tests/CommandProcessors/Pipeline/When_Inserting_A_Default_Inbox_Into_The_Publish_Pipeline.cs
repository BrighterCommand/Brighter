using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Inbox;
using Paramore.Brighter.Inbox.Handlers;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline
{
    [Collection("CommandProcessor")]
    public class CommandProcessorBuildDefaultInboxPublishTests : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly InMemoryInbox _inbox = new InMemoryInbox(new FakeTimeProvider());

        public CommandProcessorBuildDefaultInboxPublishTests()
        {
            var handler = new MyGlobalInboxEventHandler(new Dictionary<string, string>());

            var subscriberRegistry = new SubscriberRegistry();
            //This handler has no Inbox attribute
            subscriberRegistry.Add(typeof(MyEvent), typeof(MyGlobalInboxEventHandler));

            var container = new ServiceCollection();
            container.AddSingleton(handler);
            container.AddSingleton<IAmAnInboxSync>(_inbox);
            container.AddSingleton<UseInboxHandler<MyEvent>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions {HandlerLifetime = ServiceLifetime.Transient});


            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());

            var retryPolicy = Policy
                .Handle<Exception>()
                .Retry();

            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreaker(1, TimeSpan.FromMilliseconds(1));

            var inboxConfiguration = new InboxConfiguration(
                _inbox,
                InboxScope.All, //grab all the events
                onceOnly: true, //only allow once
                actionOnExists: OnceOnlyAction.Throw //throw on duplicates (we should  be the only entry after)
            );

            _commandProcessor = new CommandProcessor(
                subscriberRegistry,
                handlerFactory,
                new InMemoryRequestContextFactory(),
                new PolicyRegistry {{CommandProcessor.RETRYPOLICY, retryPolicy}, {CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy}},
                new ResiliencePipelineRegistry<string>(),
                new InMemorySchedulerFactory(),
                inboxConfiguration: inboxConfiguration
            );
            PipelineBuilder<MyEvent>.ClearPipelineCache();
        }


        [Fact]
        public void WhenInsertingADefaultInboxIntoThePublishPipeline()
        {
            //act
            var @event = new MyEvent();
            _commandProcessor.Publish(@event);

            //assert we are in, and auto-context added us under our name
            var boxed = _inbox.Exists<MyEvent>(@event.Id, typeof(MyGlobalInboxEventHandler).FullName, null, 100);
            Assert.True(boxed);
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
