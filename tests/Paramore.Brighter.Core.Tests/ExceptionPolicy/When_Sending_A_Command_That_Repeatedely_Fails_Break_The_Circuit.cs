using System;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.ExceptionPolicy.TestDoubles;
using Xunit;
using Paramore.Brighter.Policies.Handlers;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.Core.Tests.ExceptionPolicy
{
    public class CommandProcessorWithCircuitBreakerTests : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new MyCommand();
        private Exception _thirdException;
        private Exception _firstException;
        private Exception _secondException;

        public CommandProcessorWithCircuitBreakerTests()
        {
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyFailsWithDivideByZeroHandler>();

            var container = new ServiceCollection();
            container.AddSingleton<MyFailsWithDivideByZeroHandler>();
            container.AddSingleton<ExceptionPolicyHandler<MyCommand>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions {HandlerLifetime = ServiceLifetime.Transient});


            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());

            var policyRegistry = new PolicyRegistry();

            var policy = Policy
                .Handle<DivideByZeroException>()
                .CircuitBreaker(2, TimeSpan.FromMinutes(1));

            policyRegistry.Add("MyDivideByZeroPolicy", policy);

            MyFailsWithDivideByZeroHandler.ReceivedCommand = false;

            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(),
                policyRegistry, new ResiliencePipelineRegistry<string>(),new InMemorySchedulerFactory());
        }

        //We have to catch the final exception that bubbles out after retry
        [Fact]
        public void When_Sending_A_Command_That_Repeatedly_Fails_Break_The_Circuit()
        {
            //First two should be caught, and increment the count
            _firstException = Catch.Exception(() => _commandProcessor.Send(_myCommand));
            _secondException = Catch.Exception(() => _commandProcessor.Send(_myCommand));
            //this one should tell us that the circuit is broken
            _thirdException = Catch.Exception(() => _commandProcessor.Send(_myCommand));

 
            // Should send the command to the command handler
            Assert.True(MyFailsWithDivideByZeroHandler.ShouldReceive(_myCommand));
            // Should bubble up the first exception
            Assert.IsType<DivideByZeroException>(_firstException);
            // Should bubble up the second exception
            Assert.IsType<DivideByZeroException>(_secondException);
            // Should break the circuit after two fails
            Assert.IsType<BrokenCircuitException>(_thirdException);
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
