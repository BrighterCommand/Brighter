using System;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.OnceOnly.TestDoubles;
using Paramore.Brighter.Inbox.Exceptions;
using Polly.Registry;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;
using Paramore.Brighter.Inbox.Handlers;

namespace Paramore.Brighter.Core.Tests.OnceOnly
{
    public class OnceOnlyAttributeTests 
    {
        private readonly MyCommand _command;
        private readonly IAmAnInbox _inbox;
        private readonly IAmACommandProcessor _commandProcessor;
        
        public OnceOnlyAttributeTests()
        {
            _inbox = new InMemoryInbox();
            
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyStoredCommandHandler>();

            var container = new ServiceCollection();
            container.AddTransient<MyStoredCommandHandler>();
            container.AddSingleton(_inbox);
            container.AddTransient<UseInboxHandler<MyCommand>>();

            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());

            _command = new MyCommand {Value = "My Test String"};

            _commandProcessor = new CommandProcessor(registry, (IAmAHandlerFactory)handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry());
        }

        [Fact]
        public void When_Handling_A_Command_Only_Once()
        {
            _commandProcessor.Send(_command);

            Exception ex = Assert.Throws<OnceOnlyException>(() => _commandProcessor.Send(_command));
            
            Assert.Equal($"A command with id {_command.Id} has already been handled", ex.Message);
        }
        
    }
}
