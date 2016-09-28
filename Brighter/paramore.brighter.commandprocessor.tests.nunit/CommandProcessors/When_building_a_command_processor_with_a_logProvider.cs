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

using NUnit.Specifications;
using nUnitShouldAdapter;
using paramore.brighter.commandprocessor.tests.nunit.CommandProcessors.TestDoubles;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor;

namespace paramore.commandprocessor.tests.nunit.CommandProcessors
{
    [Subject(typeof(CommandProcessorBuilder))]
    public class When_Building_A_Command_Processor_With_A_LogProvider : NUnit.Specifications.ContextSpecification
    {
        private static CommandProcessor s_commandProcessor;
        private static readonly MyLogWritingCommand s_myCommand = new MyLogWritingCommand();
        private static string handlerLogMessage = "testLogMessage";

        private Establish _context = () =>
        {
            LogProvider.SetCurrentLogProvider(new FakeLogProvider());
            var registry = new SubscriberRegistry();
            registry.Register<MyLogWritingCommand, MyLogWritingCommandHandler>();
            var handlerFactory = new TestHandlerFactory<MyLogWritingCommand, MyLogWritingCommandHandler>(() => new MyLogWritingCommandHandler(handlerLogMessage));

            s_commandProcessor = CommandProcessorBuilder.With()
                .Handlers(new HandlerConfiguration(registry, handlerFactory))
                .DefaultPolicy()
                .NoTaskQueues()
                .RequestContextFactory(new InMemoryRequestContextFactory())
                .Build();
        };

        private Because _of = () => s_commandProcessor.Send(s_myCommand);

        private It _should_log_handler_message_in_provider_passed_to_command_bprocessor_builder = () => FakeLogProvider.LoggedMessages.ShouldContain(handlerLogMessage);
    }
}


