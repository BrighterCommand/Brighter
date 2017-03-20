using Nito.AsyncEx;
using Xunit;
using Paramore.Brighter.Tests.EventSourcing.TestDoubles;
using Paramore.Brighter.Tests.TestDoubles;
using TinyIoC;

namespace Paramore.Brighter.Tests.EventSourcing
{
    public class CommandProcessorUsingCommandStoreAsyncTests
    {
        private MyCommand _command;
        private IAmACommandStoreAsync _commandStore;
        private IAmACommandProcessor _commandProcessor;

        public CommandProcessorUsingCommandStoreAsyncTests()
        {
            _commandStore = new InMemoryCommandStore();

            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyStoredCommandHandlerAsync>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactoryAsync(container);
            container.Register<IHandleRequestsAsync<MyCommand>, MyStoredCommandHandlerAsync>();
            container.Register<IAmACommandStoreAsync>(_commandStore);

            _command = new MyCommand {Value = "My Test String"};

            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry());
        }

        [Fact]
        public void When_Handling_A_Command_With_A_Command_Store_Enabled_Async()
        {
            AsyncContext.Run(async () => await _commandProcessor.SendAsync(_command));

           // should_store_the_command_to_the_command_store
            Assert.AreEqual(_command.Value, _commandStore.GetAsync<MyCommand>(_command.Id).Result.Value);
        }
    }
}
