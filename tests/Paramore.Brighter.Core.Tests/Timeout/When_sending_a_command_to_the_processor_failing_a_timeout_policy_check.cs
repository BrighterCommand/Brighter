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
using System.Linq;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.Timeout.Test_Doubles;
using Xunit;
using Polly.Registry;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Policies.Handlers;

namespace Paramore.Brighter.Core.Tests.Timeout
{
    [Trait("Fragile", "CI")]
    public class TimeoutHandlerFailsCheckTests : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new MyCommand();
        private AggregateException _thrownException;

        public TimeoutHandlerFailsCheckTests()
        {
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyFailsDueToTimeoutHandler>();

            var container = new ServiceCollection();
            container.AddTransient<MyFailsDueToTimeoutHandler>();
            container.AddTransient<TimeoutPolicyHandler<MyCommand>>();

            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());
            
           _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), 
               new PolicyRegistry(), new ResiliencePipelineRegistry<string>(),new InMemorySchedulerFactory());
        }

        //We have to catch the final exception that bubbles out after retry
        [Fact(Skip="Replace with Polly Timeout")]
        public void When_Sending_A_Command_To_The_Processor_Failing_A_Timeout_Policy_Check()
        {
            _thrownException = (AggregateException)Catch.Exception(() => _commandProcessor.Send(_myCommand));

            //_should_throw_a_timeout_exception
            Assert.IsType<TimeoutException>(_thrownException.Flatten().InnerExceptions.First());
            //_should_signal_that_a_timeout_occured_and_handler_should_be_cancelled
            Assert.True(_myCommand.WasCancelled);
            //_should_not_run_to_completion
            Assert.False(_myCommand.TaskCompleted);
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
