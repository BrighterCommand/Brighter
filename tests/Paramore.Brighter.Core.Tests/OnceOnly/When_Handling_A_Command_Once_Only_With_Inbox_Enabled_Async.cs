using System;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.OnceOnly.TestDoubles;
using Paramore.Brighter.Inbox.Exceptions;
using Polly.Registry;
using TinyIoC;
using Xunit;

namespace Paramore.Brighter.Core.Tests.OnceOnly
{
    public class OnceOnlyAttributeAsyncTests
    {
        private readonly MyCommand _command;
        private readonly IAmAnInboxAsync _inbox;
        private readonly IAmACommandProcessor _commandProcessor;
        
        public OnceOnlyAttributeAsyncTests()
        {
            _inbox = new InMemoryInbox();

            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyStoredCommandHandlerAsync>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactoryAsync(container);
            container.Register<IHandleRequestsAsync<MyCommand>, MyStoredCommandHandlerAsync>();
            container.Register(_inbox);

            _command = new MyCommand {Value = "My Test String"};

            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry());
  
        }

        [Fact]
        public async Task When_Handling_A_Command_Only_Once()
        {
            await _commandProcessor.SendAsync(_command);
            
            Exception ex = await Assert.ThrowsAsync<OnceOnlyException>(() => _commandProcessor.SendAsync(_command));
            
            Assert.Equal($"A command with id {_command.Id} has already been handled", ex.Message);
 
        }
    }
}
