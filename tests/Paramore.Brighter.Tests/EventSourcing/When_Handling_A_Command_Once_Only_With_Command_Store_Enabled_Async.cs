using System.Threading.Tasks;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Tests.EventSourcing.TestDoubles;
using TinyIoC;
using Xunit;

namespace Paramore.Brighter.Tests.EventSourcing
{
    public class OnceOnlyAttributeAsyncTests
    {
        private readonly MyCommand _command;
        private readonly IAmACommandStoreAsync _commandStore;
        private readonly IAmACommandProcessor _commandProcessor;
        
        public OnceOnlyAttributeAsyncTests()
        {
            _commandStore = new InMemoryCommandStore();

            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyStoredCommandHandlerAsync>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactoryAsync(container);
            container.Register<IHandleRequestsAsync<MyCommand>, MyStoredCommandHandlerAsync>();
            container.Register(_commandStore);

            _command = new MyCommand {Value = "My Test String"};

            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry());
  
        }

        [Fact]
        public async Task When_Handling_A_Command_Only_Once()
        {
            await _commandProcessor.SendAsync(_command);

            var exception = await Record.ExceptionAsync(() => _commandProcessor.SendAsync(_command));
            
            Assert.Null(exception);
        }
    }
}
