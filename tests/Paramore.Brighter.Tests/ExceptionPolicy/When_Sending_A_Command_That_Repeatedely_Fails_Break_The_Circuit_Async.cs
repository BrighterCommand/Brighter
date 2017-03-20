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
using FluentAssertions;
using Nito.AsyncEx;
using Xunit;
using Paramore.Brighter.Policies.Handlers;
using Paramore.Brighter.Tests.ExceptionPolicy.TestDoubles;
using Paramore.Brighter.Tests.TestDoubles;
using Polly;
using Polly.CircuitBreaker;
using TinyIoC;

namespace Paramore.Brighter.Tests.ExceptionPolicy
{
    public class CommandProcessorWithCircuitBreakerAsyncTests
    {
        private CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new MyCommand();
        private Exception _thirdException;
        private Exception _firstException;
        private Exception _secondException;

        public CommandProcessorWithCircuitBreakerAsyncTests()
        {
            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyFailsWithDivideByZeroHandlerAsync>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactoryAsync(container);
            container.Register<IHandleRequestsAsync<MyCommand>, MyFailsWithDivideByZeroHandlerAsync>().AsSingleton();
            container.Register<IHandleRequestsAsync<MyCommand>, ExceptionPolicyHandlerAsync<MyCommand>>().AsSingleton();

            var policyRegistry = new PolicyRegistry();

            var policy = Policy
                .Handle<DivideByZeroException>()
                .CircuitBreakerAsync(2, TimeSpan.FromMinutes(1));

            policyRegistry.Add("MyDivideByZeroPolicy", policy);

            MyFailsWithDivideByZeroHandlerAsync.ReceivedCommand = false;

            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), policyRegistry);
        }

        //We have to catch the final exception that bubbles out after retry
        [Fact]
        public void When_Sending_A_Command_That_Repeatedely_Fails_Break_The_Circuit_Async()
        {
            //First two should be caught, and increment the count
            _firstException = Catch.Exception(() => AsyncContext.Run(async () => await _commandProcessor.SendAsync(_myCommand)));
            _secondException = Catch.Exception(() => AsyncContext.Run(async () => await _commandProcessor.SendAsync(_myCommand)));
            //this one should tell us that the circuit is broken
            _thirdException = Catch.Exception(() => AsyncContext.Run(async () => await _commandProcessor.SendAsync(_myCommand)));

            //_should_send_the_command_to_the_command_handler
            MyFailsWithDivideByZeroHandlerAsync.ShouldReceive(_myCommand).Should().BeTrue();
            //_should_bubble_up_the_first_exception
            _firstException.Should().BeOfType<DivideByZeroException>();
            //_should_bubble_up_the_second_exception
            _secondException.Should().BeOfType<DivideByZeroException>();
            //_should_break_the_circuit_after_two_fails
            _thirdException.Should().BeOfType<BrokenCircuitException>();
        }

   }
}
