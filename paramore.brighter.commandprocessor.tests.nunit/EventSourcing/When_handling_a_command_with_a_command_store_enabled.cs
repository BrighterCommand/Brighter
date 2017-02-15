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

using NUnit.Specifications;
using nUnitShouldAdapter;
using NUnit.Framework;
using paramore.brighter.commandprocessor.tests.nunit.CommandProcessors.TestDoubles;
using paramore.brighter.commandprocessor.tests.nunit.EventSourcing.TestDoubles;
using TinyIoC;

namespace paramore.brighter.commandprocessor.tests.nunit.EventSourcing
{
    [TestFixture]
    public class CommandProcessorUsingCommandStoreTests
    {
        private MyCommand _command;
        private IAmACommandStore _commandstore;
        private IAmACommandProcessor _commandProcessor;

        [SetUp]
        public void Establish()
        {
            _commandstore = new InMemoryCommandStore();

            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyStoredCommandHandler>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactory(container);
            container.Register<IHandleRequests<MyCommand>, MyStoredCommandHandler>();
            container.Register<IAmACommandStore>(_commandstore);

            _command = new MyCommand {Value = "My Test String"};

            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry());

        }

        [Test]
        public void When_Handling_A_Command_With_A_Command_Store_Enabled()
        {
            _commandProcessor.Send(_command);

            //should_store_the_command_to_the_command_store
            _commandstore.Get<MyCommand>(_command.Id).Value.ShouldEqual(_command.Value);
        }
    }
}
