using System;
using System.Threading.Tasks;
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
    public class CommandProcessorWithCircuitBreakerAsyncTests : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new MyCommand();
        private Exception? _thirdException;
        private Exception? _firstException;
        private Exception? _secondException;

        public CommandProcessorWithCircuitBreakerAsyncTests()
        {
            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyFailsWithDivideByZeroHandlerAsync>();

            var container = new ServiceCollection();
            container.AddSingleton<MyFailsWithDivideByZeroHandlerAsync>();
            container.AddSingleton<ExceptionPolicyHandlerAsync<MyCommand>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions {HandlerLifetime = ServiceLifetime.Transient});


            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());


            var policyRegistry = new PolicyRegistry();

            var policy = Policy
                .Handle<DivideByZeroException>()
                .CircuitBreakerAsync(2, TimeSpan.FromMinutes(1));

            policyRegistry.Add("MyDivideByZeroPolicy", policy);

            MyFailsWithDivideByZeroHandlerAsync.ReceivedCommand = false;

            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), policyRegistry, new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
        }

        //We have to catch the final exception that bubbles out after retry
        [Fact]
        public async Task When_Sending_A_Command_That_Repeatedly_Fails_Break_The_Circuit_Async()
        {
            //First two should be caught, and increment the count
            _firstException = await Catch.ExceptionAsync(async () => await _commandProcessor.SendAsync(_myCommand));
            _secondException = await Catch.ExceptionAsync(async () => await _commandProcessor.SendAsync(_myCommand));
            //this one should tell us that the circuit is broken
            _thirdException = await Catch.ExceptionAsync(async () => await _commandProcessor.SendAsync(_myCommand));


            // Should send the command to the command handler
            Assert.True(MyFailsWithDivideByZeroHandlerAsync.ShouldReceive(_myCommand));
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
