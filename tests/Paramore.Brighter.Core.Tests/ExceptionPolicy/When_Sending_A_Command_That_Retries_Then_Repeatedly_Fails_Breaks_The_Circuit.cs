using System;
using FluentAssertions;
using FluentAssertions.Extensions;
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
                .WaitAndRetry(new[] { 10.Milliseconds(), 20.Milliseconds(), 30.Milliseconds() },
                    (exception, timeSpan) =>
                    {
                        _retryCount++;
                    });

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
                policyRegistry, new InMemorySchedulerFactory());
        }

        [Fact]
        public void When_Sending_A_Command_That_Retries_Then_Repeatedly_Fails_Breaks_The_Circuit()
        {
            //First two should be caught, and increment the count
            _firstException = Catch.Exception(() => _commandProcessor.Send(new MyCommand()));
            //should have retried three times
            _retryCount.Should().Be(3);
            _retryCount = 0;
            _secondException = Catch.Exception(() => _commandProcessor.Send(new MyCommand()));
            //should have retried three times
            _retryCount.Should().Be(3);
            _retryCount = 0;

            //this one should tell us that the circuit is broken
            _thirdException = Catch.Exception(() => _commandProcessor.Send(new MyCommand()));
            //should have retried three times
            _retryCount.Should().Be(0);

            //_should bubble up the first_exception
            _firstException.Should().BeOfType<DivideByZeroException>();
            //_should bubble up the second exception
            _secondException.Should().BeOfType<DivideByZeroException>();
            //_should bubble up the circuit breaker exception
            _thirdException.Should().BeOfType<BrokenCircuitException>();
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
