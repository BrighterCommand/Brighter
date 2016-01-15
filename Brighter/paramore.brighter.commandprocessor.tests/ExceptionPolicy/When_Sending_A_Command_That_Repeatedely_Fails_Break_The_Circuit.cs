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
using FakeItEasy;
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

using Machine.Specifications;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.policy.Handlers;
using Polly;
using Polly.CircuitBreaker;
using TinyIoC;
using paramore.brighter.commandprocessor;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;
using paramore.commandprocessor.tests.ExceptionPolicy.TestDoubles;

namespace paramore.commandprocessor.tests.ExceptionPolicy
{
    [Subject(typeof(ExceptionPolicyHandler<>))]
    public class When_Sending_A_Command_That_Repeatedely_Fails_Break_The_Circuit
    {
        private static CommandProcessor s_commandProcessor;
        private static readonly MyCommand s_myCommand = new MyCommand();
        private static Exception s_thirdException;
        private static Exception s_firstException;
        private static Exception s_secondException;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();

            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyFailsWithDivideByZeroHandler>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactory(container);
            container.Register<IHandleRequests<MyCommand>, MyFailsWithDivideByZeroHandler>().AsSingleton();
            container.Register<IHandleRequests<MyCommand>, ExceptionPolicyHandler<MyCommand>>().AsSingleton();
            container.Register<ILog>(logger);

            var policyRegistry = new PolicyRegistry();

            var policy = Policy
                .Handle<DivideByZeroException>()
                .CircuitBreaker(2, TimeSpan.FromMinutes(1));

            policyRegistry.Add("MyDivideByZeroPolicy", policy);

            MyFailsWithDivideByZeroHandler.ReceivedCommand = false;

            s_commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), policyRegistry, logger);
        };

        //We have to catch the final exception that bubbles out after retry
        private Because _of = () =>
            {
                //First two should be caught, and increment the count
                s_firstException = Catch.Exception(() => s_commandProcessor.Send(s_myCommand));
                s_secondException = Catch.Exception(() => s_commandProcessor.Send(s_myCommand));
                //this one should tell us that the circuit is broken
                s_thirdException = Catch.Exception(() => s_commandProcessor.Send(s_myCommand));
            };

        private It _should_send_the_command_to_the_command_handler = () => MyFailsWithDivideByZeroHandler.ShouldReceive(s_myCommand).ShouldBeTrue();
        private It _should_bubble_up_the_first_exception = () => s_firstException.ShouldBeOfExactType<DivideByZeroException>();
        private It _should_bubble_up_the_second_exception = () => s_secondException.ShouldBeOfExactType<DivideByZeroException>();
        private It _should_break_the_circuit_after_two_fails = () => s_thirdException.ShouldBeOfExactType<BrokenCircuitException>();
    }
}
