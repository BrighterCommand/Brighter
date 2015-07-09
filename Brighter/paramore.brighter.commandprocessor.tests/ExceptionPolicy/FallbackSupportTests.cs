﻿#region Licence
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
using FakeItEasy;
using Machine.Specifications;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.policy.Handlers;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;
using paramore.commandprocessor.tests.ExceptionPolicy.TestDoubles;
using TinyIoC;

namespace paramore.commandprocessor.tests.ExceptionPolicy
{
    public class When_raising_an_exception_on_a_handler_that_supports_fallback
    {
        private static CommandProcessor s_commandProcessor;
        private static readonly MyCommand s_myCommand = new MyCommand();

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();

            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyFailsWithFallbackDivideByZeroHandler>();
            var policyRegistry = new PolicyRegistry();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactory(container);
            container.Register<IHandleRequests<MyCommand>, MyFailsWithFallbackDivideByZeroHandler>().AsSingleton();
            container.Register<IHandleRequests<MyCommand>, FallbackPolicyHandler<MyCommand>>().AsSingleton();
            container.Register<ILog>(logger);


            MyFailsWithFallbackDivideByZeroHandler.ReceivedCommand = false;

            s_commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), policyRegistry, logger);
        };

        private Because _of = () => s_commandProcessor.Send(s_myCommand);

        private It _should_send_the_command_to_the_command_handler = () => MyFailsWithFallbackDivideByZeroHandler.ShouldReceive(s_myCommand);
        private It _should_call_the_fallback_chain = () => MyFailsWithFallbackDivideByZeroHandler.ShouldFallback(s_myCommand);
        private It _should_set_the_exeception_into_context = () => MyFailsWithFallbackDivideByZeroHandler.ShouldSetException(s_myCommand);
    }

    [Subject(typeof(CommandProcessor))]
    public class When_falling_back_on_a_command_should_log_the_pipeline
    {
        private static CommandProcessor s_commandProcessor;
        private static readonly MyCommand s_myCommand = new MyCommand();
        static ILog _logger;

        private Establish _context = () =>
        {
            _logger = A.Fake<ILog>();
            A.CallTo(_logger).WithReturnType<bool>().Returns(true);

            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyFailsWithFallbackDivideByZeroHandler>();
            var policyRegistry = new PolicyRegistry();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactory(container);
            container.Register<IHandleRequests<MyCommand>, MyFailsWithFallbackDivideByZeroHandler>().AsSingleton();
            container.Register<IHandleRequests<MyCommand>, FallbackPolicyHandler<MyCommand>>().AsSingleton();
            container.Register<ILog>(_logger);


            MyFailsWithFallbackDivideByZeroHandler.ReceivedCommand = false;

            s_commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), policyRegistry, _logger);
        };

        private Because _of = () => s_commandProcessor.Send(s_myCommand);

        It _should_log_the_fallback_event = () => A.CallTo(_logger)
            .Where(call => call.Arguments.Get<Func<string>>(1) != null
                           && call.Arguments.Get<Func<string>>(1)() == "Falling back from {0} to {1}"
                           && call.Arguments.Get<object[]>(3)[0].ToString() == "FallbackPolicyHandler`1"
                           && call.Arguments.Get<object[]>(3)[1].ToString() == "MyFailsWithFallbackDivideByZeroHandler")
            .MustHaveHappened();
    }

    [Subject(typeof(CommandProcessor))]
    public class When_falling_back_on_an_event_should_log_the_pipeline
    {
        private static CommandProcessor s_commandProcessor;
        private static readonly MyEvent s_myCommand = new MyEvent();
        static ILog _logger;

        private Establish _context = () =>
        {
            _logger = A.Fake<ILog>();
            A.CallTo(_logger).WithReturnType<bool>().Returns(true);

            var registry = new SubscriberRegistry();
            registry.Register<MyEvent, MyFailsWithFallbackDivideByZeroEventHandler>();
            var policyRegistry = new PolicyRegistry();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactory(container);
            container.Register<IHandleRequests<MyEvent>, MyFailsWithFallbackDivideByZeroEventHandler>().AsSingleton();
            container.Register<IHandleRequests<MyEvent>, FallbackPolicyHandler<MyEvent>>().AsSingleton();
            container.Register<ILog>(_logger);


            MyFailsWithFallbackDivideByZeroHandler.ReceivedCommand = false;

            s_commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), policyRegistry, _logger);
        };

        private Because _of = () => s_commandProcessor.Send(s_myCommand);

        It _should_log_the_fallback_event = () => A.CallTo(_logger)
            .Where(call => call.Arguments.Get<Func<string>>(1) != null
                           && call.Arguments.Get<Func<string>>(1)() == "Falling back from {0} to {1}"
                           && call.Arguments.Get<object[]>(3)[0].ToString() == "FallbackPolicyHandler`1"
                           && call.Arguments.Get<object[]>(3)[1].ToString() == "MyFailsWithFallbackDivideByZeroEventHandler")
            .MustHaveHappened();
    }

    public class When_raising_a_broken_circuit_exception_on_a_handler_that_supports_fallback
    {
        private static CommandProcessor s_commandProcessor;
        private static readonly MyCommand s_myCommand = new MyCommand();

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();

            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyFailsWithFallbackBrokenCircuitHandler>();
            var policyRegistry = new PolicyRegistry();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactory(container);
            container.Register<IHandleRequests<MyCommand>, MyFailsWithFallbackBrokenCircuitHandler>().AsSingleton();
            container.Register<IHandleRequests<MyCommand>, FallbackPolicyHandler<MyCommand>>().AsSingleton();
            container.Register<ILog>(logger);


            MyFailsWithFallbackDivideByZeroHandler.ReceivedCommand = false;

            s_commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), policyRegistry, logger);
        };

        private Because _of = () => s_commandProcessor.Send(s_myCommand);

        private It _should_send_the_command_to_the_command_handler = () => MyFailsWithFallbackBrokenCircuitHandler.ShouldReceive(s_myCommand);
        private It _should_call_the_fallback_chain = () => MyFailsWithFallbackBrokenCircuitHandler.ShouldFallback(s_myCommand);
        private It _should_set_the_exeception_into_context = () => MyFailsWithFallbackBrokenCircuitHandler.ShouldSetException(s_myCommand);
        
    }


    public class When_raising_an_exception_on_a_handler_that_only_supports_fallback_for_broken_exception
    {
        private static CommandProcessor s_commandProcessor;
        private static readonly MyCommand s_myCommand = new MyCommand();
        private static Exception s_exception;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();

            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyFailsWithUnsupportedExceptionForFallback>();
            var policyRegistry = new PolicyRegistry();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactory(container);
            container.Register<IHandleRequests<MyCommand>, MyFailsWithUnsupportedExceptionForFallback>().AsSingleton();
            container.Register<IHandleRequests<MyCommand>, FallbackPolicyHandler<MyCommand>>().AsSingleton();
            container.Register<ILog>(logger);


            MyFailsWithFallbackDivideByZeroHandler.ReceivedCommand = false;

            s_commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), policyRegistry, logger);
        };

        private Because _of = () => s_exception = Catch.Exception(() => s_commandProcessor.Send(s_myCommand));

        private It _should_send_the_command_to_the_command_handler = () => MyFailsWithUnsupportedExceptionForFallback.ShouldReceive(s_myCommand);
        private It _should_bubble_out_the_exception = () => s_exception.ShouldNotBeNull();
    }

    public class When_raising_an_exception_fallback_should_call_through_a_chain_of_handlers
    {
        private static CommandProcessor s_commandProcessor;
        private static readonly MyCommand s_myCommand = new MyCommand();

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();

            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyFailsWithFallbackMultipleHandlers>();
            var policyRegistry = new PolicyRegistry();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactory(container);
            container.Register<IHandleRequests<MyCommand>, MyFailsWithFallbackMultipleHandlers>().AsSingleton();
            container.Register<IHandleRequests<MyCommand>, FallbackPolicyHandler<MyCommand>>().AsSingleton();
            container.Register<ILog>(logger);


            MyFailsWithFallbackMultipleHandlers.ReceivedCommand = false;

            s_commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), policyRegistry, logger);
        };

        private Because _of = () => s_commandProcessor.Send(s_myCommand);

        private It _should_send_the_command_to_the_command_handler = () => MyFailsWithFallbackMultipleHandlers.ShouldReceive(s_myCommand);
        private It _should_call_the_fallback_chain = () => MyFailsWithFallbackMultipleHandlers.ShouldFallback(s_myCommand);
        private It _should_set_the_exeception_into_context = () => MyFailsWithFallbackMultipleHandlers.ShouldSetException(s_myCommand);
    }
}
