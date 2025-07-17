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
    public class CommandProcessorBuildDefaultInboxSendTests : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly ServiceProvider _provider;
        private readonly Dictionary<string, string> _receivedMessages = new();

        public CommandProcessorBuildDefaultInboxSendTests()
        {
            var subscriberRegistry = new SubscriberRegistry();
            //This handler has no Inbox attribute
            subscriberRegistry.Add(typeof(MyCommand), typeof(MyCommandHandler));

            var container = new ServiceCollection();
            container.AddTransient<MyCommandHandler>(_ => new MyCommandHandler(_receivedMessages));
            container.AddSingleton<IAmAnInboxSync>(new InMemoryInbox(new FakeTimeProvider()));
            container.AddTransient<UseInboxHandler<MyCommand>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions {HandlerLifetime = ServiceLifetime.Transient});

            _provider = container.BuildServiceProvider();
            var handlerFactory = new ServiceProviderHandlerFactory(_provider);

            var retryPolicy = Policy
                .Handle<Exception>()
                .Retry();

            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreaker(1, TimeSpan.FromMilliseconds(1));

            var inboxConfiguration = new InboxConfiguration(
                new InMemoryInbox(new FakeTimeProvider()),
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

            PipelineBuilder<MyCommand>.ClearPipelineCache();
        }


        [Fact]
        public void WhenInsertingADefaultInboxIntoTheSendPipeline()
        {
            //act
            var command = new MyCommand {Value = "Inbox Capture"};
            _commandProcessor.Send(command);

            //assert we are in, and auto-context added us under our name
            var inbox = _provider.GetService<IAmAnInboxSync>();
            Assert.NotNull(inbox);
            var boxed = inbox.Exists<MyCommand>(command.Id, typeof(MyCommandHandler).FullName, null, 100);
            Assert.True(boxed);
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
