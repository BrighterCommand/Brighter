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
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.ExceptionPolicy.TestDoubles;
using Xunit;
using Paramore.Brighter.Policies.Handlers;
using Polly.Registry;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.Core.Tests.ExceptionPolicy
{
    public class FallbackHandlerOnExceptionTests : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new MyCommand();

        public FallbackHandlerOnExceptionTests()
        {
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyFailsWithFallbackDivideByZeroHandler>();
            var policyRegistry = new PolicyRegistry();

            var container = new ServiceCollection();
            container.AddSingleton<MyFailsWithFallbackDivideByZeroHandler>();
            container.AddSingleton<FallbackPolicyHandler<MyCommand>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions {HandlerLifetime = ServiceLifetime.Transient});
            
            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());
            
            MyFailsWithFallbackDivideByZeroHandler.ReceivedCommand = false;

            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), policyRegistry,  new ResiliencePipelineRegistry<string>(),new InMemorySchedulerFactory());
        }

        [Fact]
        public void When_Raising_An_Exception_Can_Fallback()
        {
            _commandProcessor.Send(_myCommand);

            //_should_send_the_command_to_the_command_handler
            MyFailsWithFallbackDivideByZeroHandler.ShouldReceive(_myCommand);
            //_should_call_the_fallback_chain
            MyFailsWithFallbackDivideByZeroHandler.ShouldFallback(_myCommand);
            //_should_set_the_exception_into_context
            MyFailsWithFallbackDivideByZeroHandler.ShouldSetException(_myCommand);
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
