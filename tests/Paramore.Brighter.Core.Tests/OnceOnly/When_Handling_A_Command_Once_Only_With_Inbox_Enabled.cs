using System;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.OnceOnly.TestDoubles;
using Paramore.Brighter.Inbox.Exceptions;
using Polly.Registry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Inbox.Handlers;

namespace Paramore.Brighter.Core.Tests.OnceOnly
{
    public class OnceOnlyAttributeTests
    {
        private readonly MyCommand _command;
        private readonly IAmAnInboxSync _inbox;
        private readonly IAmACommandProcessor _commandProcessor;
        public OnceOnlyAttributeTests()
        {
            _inbox = new InMemoryInbox(new FakeTimeProvider());
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyStoredCommandHandler>();
            var container = new ServiceCollection();
            container.AddTransient<MyStoredCommandHandler>();
            container.AddSingleton(_inbox);
            container.AddTransient<UseInboxHandler<MyCommand>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });
            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());
            _command = new MyCommand
            {
                Value = "My Test String"
            };
            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
        }

        [Test]
        public async Task When_Handling_A_Command_Only_Once()
        {
            _commandProcessor.Send(_command);
            var ex = await Assert.That(() => _commandProcessor.Send(_command)).ThrowsExactly<OnceOnlyException>();
            await Assert.That(ex!.Message).IsEqualTo($"A command with id {_command.Id} has already been handled");
        }
    }
}