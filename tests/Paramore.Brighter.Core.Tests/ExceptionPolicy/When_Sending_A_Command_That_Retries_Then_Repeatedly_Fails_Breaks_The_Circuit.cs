using System;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.ExceptionPolicy.TestDoubles;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Policies.Handlers;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.ExceptionPolicy
{
    [Collection("CommandProcessor")]
     public class CommandProcessorWithBothRetryAndCircuitBreaker : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private Exception _thirdException;
        private Exception _firstException;
        private Exception _secondException;
        private int _retryCount;
        private Polly.Context _context;

        public CommandProcessorWithBothRetryAndCircuitBreaker()
        {
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyMultiplePoliciesFailsWithDivideByZeroHandler>();

            var container = new ServiceCollection();
            container.AddSingleton<MyMultiplePoliciesFailsWithDivideByZeroHandler>();
            container.AddSingleton<ExceptionPolicyHandler<MyCommand>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions()
            {
                HandlerLifetime = ServiceLifetime.Transient
            });


            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());

            var policyRegistry = new PolicyRegistry();

            var retryPolicy = Policy
                .Handle<DivideByZeroException>()
                .WaitAndRetry([
                        TimeSpan.FromMilliseconds(10), 
                        TimeSpan.FromMilliseconds(20), 
                        TimeSpan.FromMilliseconds(30)
                    ],
                    (exception, timeSpan) =>
                        _retryCount++
                 );

            var breakerPolicy = Policy.Handle<DivideByZeroException>()
                .CircuitBreaker(
                    exceptionsAllowedBeforeBreaking: 2,
                    durationOfBreak: TimeSpan.FromSeconds(30),
                    onBreak: (exception, timespan, context) =>
                    {
                    },
                    onReset: context =>  _context = context
                );

            policyRegistry.Add("MyDivideByZeroRetryPolicy", retryPolicy);
            policyRegistry.Add("MyDivideByZeroBreakerPolicy", breakerPolicy);


            MyMultiplePoliciesFailsWithDivideByZeroHandler.ReceivedCommand = false;

            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(),
                policyRegistry, new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
        }

        [Fact]
        public void When_Sending_A_Command_That_Retries_Then_Repeatedly_Fails_Breaks_The_Circuit()
        {
            // First two should be caught, and increment the count
            _firstException = Catch.Exception(() => _commandProcessor.Send(new MyCommand()));
            // Should have retried three times
            Assert.Equal(3, _retryCount);
            _retryCount = 0;
            _secondException = Catch.Exception(() => _commandProcessor.Send(new MyCommand()));
            // Should have retried three times
            Assert.Equal(3, _retryCount);
            _retryCount = 0;

            // This one should tell us that the circuit is broken
            _thirdException = Catch.Exception(() => _commandProcessor.Send(new MyCommand()));
            // Should not retry
            Assert.Equal(0, _retryCount);

            // Should bubble up the first exception
            Assert.IsType<DivideByZeroException>(_firstException);
            // Should bubble up the second exception
            Assert.IsType<DivideByZeroException>(_secondException);
            // Should bubble up the circuit breaker exception
            Assert.IsType<BrokenCircuitException>(_thirdException);
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
