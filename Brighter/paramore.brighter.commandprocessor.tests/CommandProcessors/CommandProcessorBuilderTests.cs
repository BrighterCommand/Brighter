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

using System;
using System.Collections.Generic;
using Machine.Specifications;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;

namespace paramore.commandprocessor.tests.CommandProcessorBuilderTests
{
    [Subject("Test Builder and LibLog with default handler ctor")]
    public class When_building_a_command_processor_with_a_logProvider
    {
        private static CommandProcessor s_commandProcessor;
        private static readonly MyLogWritingCommand s_myCommand = new MyLogWritingCommand();
        private static string handlerLogMessage = "testLogMessage";
        private static FakeLogProvider _customProvider;

        private Establish _context = () =>
        {
            _customProvider = new FakeLogProvider();
            var registry = new SubscriberRegistry();
            registry.Register<MyLogWritingCommand, MyLogWritingCommandHandler>();
            var handlerFactory = new TestHandlerFactory<MyLogWritingCommand, MyLogWritingCommandHandler>(() => new MyLogWritingCommandHandler(handlerLogMessage));

            s_commandProcessor = CommandProcessorBuilder.With()
                .Handlers(new HandlerConfiguration(registry, handlerFactory))
                .DefaultPolicy()
                .Logger(_customProvider)
                .NoTaskQueues()
                .RequestContextFactory(new InMemoryRequestContextFactory())
                .Build();
        };

        private Because _of = () => s_commandProcessor.Send(s_myCommand);

        private It _should_log_handler_message_in_provider_passed_to_command_bprocessor_builder = () => FakeLogProvider.LoggedMessages.ShouldContain(handlerLogMessage);
    }

    [Subject("Test Builder and LibLog with no logger")]
    public class When_building_a_command_processor_with_no_logProvider
    {
        private static CommandProcessor s_commandProcessor;
        private static readonly MyLogWritingCommand s_myCommand = new MyLogWritingCommand();
        private static string handlerLogMessage = "testLogMessage";
        private static Exception s_exception;

        private Establish _context = () =>
        {
            var registry = new SubscriberRegistry();
            registry.Register<MyLogWritingCommand, MyLogWritingCommandHandler>();
            var handlerFactory = new TestHandlerFactory<MyLogWritingCommand, MyLogWritingCommandHandler>(() => new MyLogWritingCommandHandler(handlerLogMessage));

            s_commandProcessor = CommandProcessorBuilder.With()
                .Handlers(new HandlerConfiguration(registry, handlerFactory))
                .DefaultPolicy()
                .NullLogger()
                .NoTaskQueues()
                .RequestContextFactory(new InMemoryRequestContextFactory())
                .Build();
        };

        private Because _of = () => s_exception = Catch.Exception(() => s_commandProcessor.Send(s_myCommand));

        private It _should_not_error = () => s_exception.ShouldBeNull();
    }

    [Subject("Test Builder and LibLog with no logger")]
    public class When_building_a_command_processor_with_null_logger
    {
        private static CommandProcessor s_commandProcessor;
        private static readonly MyLogWritingCommand s_myCommand = new MyLogWritingCommand();
        private static string handlerLogMessage = "testLogMessage";
        private static Exception s_exception;

        private Establish _context = () =>
        {
            var registry = new SubscriberRegistry();
            registry.Register<MyLogWritingCommand, MyLogWritingCommandHandler>();
            var handlerFactory = new TestHandlerFactory<MyLogWritingCommand, MyLogWritingCommandHandler>(() => new MyLogWritingCommandHandler(handlerLogMessage));

            s_commandProcessor = CommandProcessorBuilder.With()
                .Handlers(new HandlerConfiguration(registry, handlerFactory))
                .DefaultPolicy()
                .NullLogger()
                .NoTaskQueues()
                .RequestContextFactory(new InMemoryRequestContextFactory())
                .Build();
        };

        private Because _of = () => s_exception = Catch.Exception(() => s_commandProcessor.Send(s_myCommand));

        private It _should_not_error = () => s_exception.ShouldBeNull();
    }
    internal class MyLogWritingCommandHandler : RequestHandler<MyLogWritingCommand>
    {
        private readonly string _handlerLogMessage;
        private static MyLogWritingCommand s_command;

        public MyLogWritingCommandHandler(string handlerLogMessage)
        {
            _handlerLogMessage = handlerLogMessage;
        }

        public override MyLogWritingCommand Handle(MyLogWritingCommand command)
        {
            s_command = command;
            logger.Log(LogLevel.Debug, () => _handlerLogMessage);

            return base.Handle(command);
        }

        public static bool Shouldreceive(MyLogWritingCommand expectedCommand)
        {
            return (s_command != null) && (expectedCommand.Id == s_command.Id);
        }

    }

    internal class MyLogWritingCommand : Command
    {
        public MyLogWritingCommand() : base(Guid.NewGuid()){}
    }

    internal class FakeLogProvider : ILogProvider
    {
        public static List<string> LoggedMessages = new List<string>();

        private static bool GetLogger(LogLevel logLevel, Func<string> messageFunc, Exception exception, params object[] formatParameters)
        {
            if (messageFunc != null)
            {
                LoggedMessages.Add(messageFunc.Invoke());                
            }
            return true;
        }

        public Logger GetLogger(string name)
        {
            return GetLogger;
        }

        public IDisposable OpenNestedContext(string message)
        {
            return null;
        }

        public IDisposable OpenMappedContext(string key, string value)
        {
            return null;
        }
    }
}


