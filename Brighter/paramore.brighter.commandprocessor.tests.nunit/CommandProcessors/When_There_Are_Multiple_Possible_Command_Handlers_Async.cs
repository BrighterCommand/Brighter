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
using FakeItEasy;
using nUnitShouldAdapter;
using Nito.AsyncEx;
using NUnit.Specifications;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.tests.nunit.CommandProcessors.TestDoubles;
using TinyIoC;

namespace paramore.brighter.commandprocessor.tests.nunit.CommandProcessors
{
    [Subject(typeof(CommandProcessor))]
    public class When_There_Are_Multiple_Possible_Command_Handlers_Async : ContextSpecification
    {
        private static CommandProcessor s_commandProcessor;
        private static readonly MyCommand s_myCommand = new MyCommand();
        private static Exception s_exception;

        private Establish _context = () =>
        {
            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyCommandHandlerAsync>();
            registry.RegisterAsync<MyCommand, MyImplicitHandlerAsync>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactoryAsync(container);
            container.Register<IHandleRequestsAsync<MyCommand>, MyCommandHandlerAsync>("DefaultHandler");
            container.Register<IHandleRequestsAsync<MyCommand>, MyImplicitHandlerAsync>("ImplicitHandler");
            container.Register<IHandleRequestsAsync<MyCommand>, MyLoggingHandlerAsync<MyCommand>>();
            container.Register<ILog>(A.Fake<ILog>());

            s_commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry());
        };

        //Ignore any errors about adding System.Runtime from the IDE. See https://social.msdn.microsoft.com/Forums/en-US/af4dc0db-046c-4728-bfe0-60ceb93f7b9f/vs2012net-45-rc-compiler-error-when-using-actionblock-missing-reference-to?forum=tpldataflow
        private Because _of = () => s_exception = Catch.Exception(() => AsyncContext.Run(async () => await s_commandProcessor.SendAsync(s_myCommand)));

        private It _should_fail_because_multiple_receivers_found = () => s_exception.ShouldBeAssignableTo(typeof(ArgumentException));
        private It _should_have_an_error_message_that_tells_you_why = () => s_exception
            .ShouldContainErrorMessage("More than one handler was found for the typeof command paramore.brighter.commandprocessor.tests.nunit.CommandProcessors.TestDoubles.MyCommand - a command should only have one handler.");

    }
}
