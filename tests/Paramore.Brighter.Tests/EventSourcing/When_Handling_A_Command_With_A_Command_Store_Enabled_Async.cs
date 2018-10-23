using System;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using Xunit;
using Paramore.Brighter.Tests.EventSourcing.TestDoubles;
using TinyIoC;

namespace Paramore.Brighter.Tests.EventSourcing
{
    public class CommandProcessorUsingCommandStoreAsyncTests
    {
        private readonly MyCommand _command;
        private readonly IAmACommandStoreAsync _commandStore;
        private readonly IAmACommandProcessor _commandProcessor;
        private readonly string _contextKey;

        public CommandProcessorUsingCommandStoreAsyncTests()
        {
            _commandStore = new InMemoryCommandStore();

            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyStoredCommandHandlerAsync>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactoryAsync(container);
            container.Register<IHandleRequestsAsync<MyCommand>, MyStoredCommandHandlerAsync>();
            container.Register<IHandleRequestsAsync<MyCommandToFail>, MyStoredCommandToFailHandlerAsync>();
            container.Register(_commandStore);

            _contextKey = typeof(MyStoredCommandHandlerAsync).FullName;

            _command = new MyCommand {Value = "My Test String"};

            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry());
        }

        [Fact]
        public async Task When_Handling_A_Command_With_A_Command_Store_Enabled_Async()
        {
            await _commandProcessor.SendAsync(_command);

           // should_store_the_command_to_the_command_store
            _commandStore.GetAsync<MyCommand>(_command.Id, _contextKey).Result.Value.Should().Be(_command.Value);
        }

        [Fact]
        public async Task Command_Is_Not_Stored_If_The_Handler_Is_Not_Succesful()
        {
            Guid id = Guid.NewGuid();
            Catch.Exception(() => _commandProcessor.Send(new MyCommandToFail() { Id = id }));

            _commandStore.ExistsAsync<MyCommandToFail>(id, typeof(MyStoredCommandToFailHandlerAsync).FullName).Result.Should().BeFalse();
        }
    }
}
