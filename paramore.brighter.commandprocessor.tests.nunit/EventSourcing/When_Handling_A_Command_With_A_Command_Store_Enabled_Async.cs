using Nito.AsyncEx;
using NUnit.Framework;
using paramore.brighter.commandprocessor.tests.nunit.CommandProcessors.TestDoubles;
using paramore.brighter.commandprocessor.tests.nunit.EventSourcing.TestDoubles;
using TinyIoC;

namespace paramore.brighter.commandprocessor.tests.nunit.EventSourcing
{
    [TestFixture]
    public class CommandProcessorUsingCommandStoreAsyncTests
    {
        private MyCommand _command;
        private IAmACommandStoreAsync _commandStore;
        private IAmACommandProcessor _commandProcessor;

        [SetUp]
        public void Establish()
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

        [Test]
        public void When_Handling_A_Command_With_A_Command_Store_Enabled_Async()
        {
            AsyncContext.Run(async () => await _commandProcessor.SendAsync(_command));

           // should_store_the_command_to_the_command_store
            _commandStore.GetAsync<MyCommand>(_command.Id).Result.Value.ShouldEqual(_command.Value);
        }
    }
}
