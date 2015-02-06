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
using Common.Logging;
using FakeItEasy;
using FluentAssertions;
using Machine.Specifications;
using paramore.brighter.commandprocessor.policy.Handlers;
using Polly;
using Polly.CircuitBreaker;
using TinyIoC;
using paramore.brighter.commandprocessor;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;
using paramore.commandprocessor.tests.ExceptionPolicy.TestDoubles;

namespace paramore.commandprocessor.tests.ExceptionPolicy
{
   [Subject("Basic policy on a handler")]
    public class When_sending_a_command_to_the_processor_with_a_retry_policy_check
    {
        static CommandProcessor commandProcessor;
        static readonly MyCommand myCommand = new MyCommand();
       static int retryCount;

        Establish context = () =>
        {
            var logger = A.Fake<ILog>();

            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyFailsWithDivideByZeroHandler >();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactory(container);
            container.Register<IHandleRequests<MyCommand>, MyFailsWithDivideByZeroHandler >().AsSingleton();
            container.Register<IHandleRequests<MyCommand>, ExceptionPolicyHandler<MyCommand>>().AsSingleton();
            container.Register<ILog>(logger);

            var policyRegistry = new PolicyRegistry();

            var policy = Policy
                .Handle<DivideByZeroException>()
                .WaitAndRetry(new[]
                {
                    1.Seconds(),
                    2.Seconds(),
                    3.Seconds()
                }, (exception, timeSpan) => 
                {
                    retryCount++;
                });
            policyRegistry.Add("MyDivideByZeroPolicy", policy);

            MyFailsWithDivideByZeroHandler.ReceivedCommand = false;

            commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), policyRegistry, logger);

        };

       //We have to catch the final exception that bubbles out after retry
        Because of = () => Catch.Exception(() => commandProcessor.Send(myCommand));

       It should_send_the_command_to_the_command_handler = () => MyFailsWithDivideByZeroHandler.Shouldreceive(myCommand).ShouldBeTrue();
       It should_retry_three_times = () => retryCount.ShouldEqual(3);
    }

    [Subject("Basic policy on a handler")]
    public class When_sending_a_command_to_the_processor_passes_policy_check
    {
        static CommandProcessor commandProcessor;
        static readonly MyCommand myCommand = new MyCommand();
        static int retryCount;

        Establish context = () =>
        {
            var logger = A.Fake<ILog>();

            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyDoesNotFailPolicyHandler >();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactory(container);
            container.Register<IHandleRequests<MyCommand>, MyDoesNotFailPolicyHandler >("MyDoesNotFailPolicyHandler");
            container.Register<IHandleRequests<MyCommand>, ExceptionPolicyHandler<MyCommand>>("MyExceptionPolicyHandler");
            container.Register<ILog>(logger);

            var policyRegistry = new PolicyRegistry();

            var policy = Policy
                .Handle<DivideByZeroException>()
                .WaitAndRetry(new[]
                {
                    1.Seconds(),
                    2.Seconds(),
                    3.Seconds()
                }, (exception, timeSpan) => 
                {
                    retryCount++;
                });
            policyRegistry.Add("MyDivideByZeroPolicy", policy);

            MyDoesNotFailPolicyHandler.ReceivedCommand = false;

            commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), policyRegistry, logger);

        };

       //We have to catch the final exception that bubbles out after retry
        Because of = () => commandProcessor.Send(myCommand);

       It should_send_the_command_to_the_command_handler = () => MyDoesNotFailPolicyHandler .Shouldreceive(myCommand).ShouldBeTrue();
       It should_not_retry = () => retryCount.ShouldEqual(0);
    }

    [Subject("Basic policy on a handler")]
    public class When_sending_a_command_to_the_processor_with_a_circuit_breaker
    {
        static CommandProcessor commandProcessor;
        static readonly MyCommand myCommand = new MyCommand();
        static Exception thirdException;
        static Exception firstException;
        static Exception secondException;

        Establish context = () =>
        {
            var logger = A.Fake<ILog>();

            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyFailsWithDivideByZeroHandler >();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactory(container);
            container.Register<IHandleRequests<MyCommand>, MyFailsWithDivideByZeroHandler >().AsSingleton();
            container.Register<IHandleRequests<MyCommand>, ExceptionPolicyHandler<MyCommand>>().AsSingleton();
            container.Register<ILog>(logger);

            var policyRegistry = new PolicyRegistry();

            var policy = Policy
                .Handle<DivideByZeroException>()
                .CircuitBreaker(2, TimeSpan.FromMinutes(1));

            policyRegistry.Add("MyDivideByZeroPolicy", policy);

            MyFailsWithDivideByZeroHandler.ReceivedCommand = false;

            commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), policyRegistry, logger);

        };

       //We have to catch the final exception that bubbles out after retry
        Because of = () =>
            {
                //First two should be caught, and increment the count
                firstException = Catch.Exception(() => commandProcessor.Send(myCommand));
                secondException = Catch.Exception(() => commandProcessor.Send(myCommand));
                //this one should tell us that the circuit is broken
                thirdException = Catch.Exception(() => commandProcessor.Send(myCommand));
            };

       It should_send_the_command_to_the_command_handler = () => MyFailsWithDivideByZeroHandler.Shouldreceive(myCommand).ShouldBeTrue();
       It should_bubble_up_the_first_exception = () => firstException.ShouldBeOfExactType<DivideByZeroException>();
       It should_bubble_up_the_second_exception = () => secondException.ShouldBeOfExactType<DivideByZeroException>(); 
       It should_break_the_circuit_after_two_fails = () => thirdException.ShouldBeOfExactType<BrokenCircuitException>();
    }
}
