using System;
using Paramore.Brighter.Inbox.Exceptions;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Tests.OnceOnly.TestDoubles;
using Polly.Registry;
using TinyIoC;
using Xunit;

namespace Paramore.Brighter.Tests.OnceOnly
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

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactory(container);
            container.Register<IHandleRequests<MyCommand>, MyStoredCommandHandler>();
            container.Register(_inbox);

            _command = new MyCommand {Value = "My Test String"};

            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry());
 
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
