using System;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.ExceptionPolicy.TestDoubles;
using Xunit;
using Paramore.Brighter.Policies.Handlers;
using Polly.Registry;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.Core.Tests.ExceptionPolicy
{
    public class FallbackHandlerBrokenCircuitTests : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new MyCommand();
        private Exception _exception;

        public FallbackHandlerBrokenCircuitTests()
        {
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyFailsWithUnsupportedExceptionForFallback>();
            var policyRegistry = new PolicyRegistry();

            var container = new ServiceCollection();
            container.AddSingleton<MyFailsWithUnsupportedExceptionForFallback>();
            container.AddSingleton<FallbackPolicyHandler<MyCommand>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions {HandlerLifetime = ServiceLifetime.Transient});
             

            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());


            MyFailsWithFallbackDivideByZeroHandler.ReceivedCommand = false;

            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), policyRegistry, new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
        }

        [Fact]
        public void When_A_Fallback_Is_Broken_Circuit_Only()
        {
            _exception = Catch.Exception(() => _commandProcessor.Send(_myCommand));

            //Should send the command to the command handler
            MyFailsWithUnsupportedExceptionForFallback.ShouldReceive(_myCommand);
            // Should bubble out the exception
            Assert.NotNull(_exception);
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
