using System;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.OnceOnly.TestDoubles;
using Polly.Registry;
using TinyIoC;
using Xunit;

namespace Paramore.Brighter.Core.Tests.OnceOnly
{
    public class CommandProcessorUsingInboxAsyncTests
    {
        private readonly MyCommand _command;
        private readonly IAmAnInboxAsync _inbox;
        private readonly IAmACommandProcessor _commandProcessor;
        private readonly string _contextKey;

        public CommandProcessorUsingInboxAsyncTests()
        {
            _inbox = new InMemoryInbox();

            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyStoredCommandHandlerAsync>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactoryAsync(container);
            container.Register<IHandleRequestsAsync<MyCommand>, MyStoredCommandHandlerAsync>();
            container.Register<IHandleRequestsAsync<MyCommandToFail>, MyStoredCommandToFailHandlerAsync>();
            container.Register(_inbox);

            _contextKey = typeof(MyStoredCommandHandlerAsync).FullName;

            _command = new MyCommand {Value = "My Test String"};

            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry());
        }

        [Fact]
        public async Task When_Handling_A_Command_With_A_Inbox_Enabled_Async()
        {
            await _commandProcessor.SendAsync(_command);

           // should_store_the_command_to_the_inbox
            _inbox.GetAsync<MyCommand>(_command.Id, _contextKey).Result.Value.Should().Be(_command.Value);
        }

        [Fact]
        public async Task Command_Is_Not_Stored_If_The_Handler_Is_Not_Succesful()
        {
            Guid id = Guid.NewGuid();
            Catch.Exception(() => _commandProcessor.Send(new MyCommandToFail() { Id = id }));

            var exists =
                await _inbox.ExistsAsync<MyCommandToFail>(id, typeof(MyStoredCommandToFailHandlerAsync).FullName);
            exists.Should().BeFalse();
        }
    }
}
