#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using FakeItEasy;
using Machine.Specifications;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;
using TinyIoC;

namespace paramore.commandprocessor.tests.CommandProcessors
{
    public class When_There_Are_No_Failures_Execute_All_The_Steps_In_The_Pipeline
    {
        private static CommandProcessor s_commandProcessor;
        private static readonly MyCommand s_myCommand = new MyCommand();

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyPreAndPostDecoratedHandler>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactory(container);
            container.Register<IHandleRequests<MyCommand>, MyPreAndPostDecoratedHandler>();
            container.Register<IHandleRequests<MyCommand>, MyValidationHandler<MyCommand>>();
            container.Register<IHandleRequests<MyCommand>, MyLoggingHandler<MyCommand>>();
            container.Register<ILog>(logger);
            s_commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), logger);
        };

        private Because _of = () => s_commandProcessor.Send(s_myCommand);

        private It _should_call_the_pre_validation_handler = () => MyValidationHandler<MyCommand>.ShouldReceive(s_myCommand).ShouldBeTrue();
        private It _should_send_the_command_to_the_command_handler = () => MyPreAndPostDecoratedHandler.ShouldReceive(s_myCommand).ShouldBeTrue();
        private It _should_call_the_post_validation_handler = () => MyLoggingHandler<MyCommand>.Shouldreceive(s_myCommand).ShouldBeTrue();
    }
}