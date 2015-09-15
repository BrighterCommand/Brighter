﻿#region Licence
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

using System.Threading.Tasks;
using FakeItEasy;
using Machine.Specifications;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;
using paramore.commandprocessor.tests.EventSourcing.TestDoubles;
using TinyIoC;

namespace paramore.commandprocessor.tests.EventSourcing
{
    public class When_handling_a_command_with_a_command_store_enabled
    {
        private static MyCommand s_command;
        private static IAmACommandStore s_commandstore;
        private static IAmACommandProcessor s_commandProcessor;

        private Establish context = () =>
        {
            var logger = A.Fake<ILog>();

            s_commandstore = new InMemoryCommandStore();

            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyStoredCommandHandler>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactory(container);
            container.Register<IHandleRequests<MyCommand>, MyStoredCommandHandler>();
            container.Register<IAmACommandStore>(s_commandstore);
            container.Register<ILog>(logger);

            s_command = new MyCommand {Value = "My Test String"};

            s_commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), logger);

        };

        private Because of = () =>
         {
             s_commandProcessor.Send(s_command);

             //we need to wait for the command store, which is asynchronous to complete writing
             Task.Delay(500);
         };

        private It should_store_the_command_to_the_command_store = () => s_commandstore.Get<MyCommand>(s_command.Id).Value.ShouldEqual(s_command.Value);
    }
}
