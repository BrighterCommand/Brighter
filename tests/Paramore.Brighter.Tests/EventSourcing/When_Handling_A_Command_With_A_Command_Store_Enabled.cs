#region Licence
/* The MIT License (MIT)
Copyright © 2015 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using FluentAssertions;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using Xunit;
using Paramore.Brighter.Tests.EventSourcing.TestDoubles;
using Polly.Registry;
using TinyIoC;

namespace Paramore.Brighter.Tests.EventSourcing
{
    public class CommandProcessorUsingCommandStoreTests
    {
        private readonly MyCommand _command;
        private readonly IAmACommandStore _commandstore;
        private readonly IAmACommandProcessor _commandProcessor;
        private readonly string _contextKey;

        public CommandProcessorUsingCommandStoreTests()
        {
            _commandstore = new InMemoryCommandStore();

            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyStoredCommandHandler>();
            registry.Register<MyCommandToFail, MyStoredCommandToFailHandler>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactory(container);
            container.Register<IHandleRequests<MyCommand>, MyStoredCommandHandler>();
            container.Register(_commandstore);

            _command = new MyCommand {Value = "My Test String"};

            _contextKey = typeof(MyStoredCommandHandler).FullName;
            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry());

        }

        [Fact]
        public void When_Handling_A_Command_With_A_Command_Store_Enabled()
        {
            _commandProcessor.Send(_command);

            //should_store_the_command_to_the_command_store
            _commandstore.Get<MyCommand>(_command.Id, _contextKey).Value.Should().Be(_command.Value);
        }

        [Fact]
        public void Command_Is_Not_Stored_If_The_Handler_Is_Not_Succesful()
        {
            Guid id = Guid.NewGuid();
            Catch.Exception(() => _commandProcessor.Send(new MyCommandToFail() { Id = id}));

            _commandstore.Exists<MyCommandToFail>(id, typeof(MyStoredCommandToFailHandler).FullName).Should().BeFalse();
        }
    }
}
