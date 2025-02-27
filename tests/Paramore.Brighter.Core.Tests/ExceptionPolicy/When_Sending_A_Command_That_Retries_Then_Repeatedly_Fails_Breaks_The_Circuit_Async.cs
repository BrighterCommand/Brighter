using System;
using System.Threading.Tasks;
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
     public class CommandProcessorWithBothRetryAndCircuitBreakerAsync : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private Exception _thirdException;
        private Exception _firstException;
        private Exception _secondException;
        private int _retryCount;
        private Polly.Context _context;

        public CommandProcessorWithBothRetryAndCircuitBreakerAsync()
        {
            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyMultiplePoliciesFailsWithDivideByZeroHandlerAsync>();

            var container = new ServiceCollection();
            container.AddSingleton<MyMultiplePoliciesFailsWithDivideByZeroHandlerAsync>();
            container.AddSingleton<ExceptionPolicyHandlerAsync<MyCommand>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions()
            {
                HandlerLifetime = ServiceLifetime.Transient
            });


            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());

            var policyRegistry = new PolicyRegistry();

            var retryPolicy = Policy
                .Handle<DivideByZeroException>()
                .WaitAndRetryAsync(new[] { 10.Milliseconds(), 20.Milliseconds(), 30.Milliseconds() },
                    (exception, timeSpan) =>
                    {
                        _retryCount++;
                    });

            var breakerPolicy = Policy.Handle<DivideByZeroException>()
                .CircuitBreakerAsync(
                    exceptionsAllowedBeforeBreaking: 2,
                    durationOfBreak: TimeSpan.FromSeconds(30),
                    onBreak: (exception, timespan, context) =>
                    {
                    },
                    onReset: context =>  _context = context
                );

            policyRegistry.Add("MyDivideByZeroRetryPolicyAsync", retryPolicy);
            policyRegistry.Add("MyDivideByZeroBreakerPolicyAsync", breakerPolicy);


            MyMultiplePoliciesFailsWithDivideByZeroHandlerAsync.ReceivedCommand = false;

            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(),
                policyRegistry, new InMemorySchedulerFactory());
        }

        [Fact]
        public async Task When_Sending_A_Command_That_Retries_Then_Repeatedly_Fails_Breaks_The_Circuit()
        {
            //First two should be caught, and increment the count
            _firstException = await Catch.ExceptionAsync(async () => await _commandProcessor.SendAsync(new MyCommand()));
            //should have retried three times
            _retryCount.Should().Be(3);
            _retryCount = 0;
            _secondException = await Catch.ExceptionAsync(async() => await _commandProcessor.SendAsync(new MyCommand()));
            //should have retried three times
            _retryCount.Should().Be(3);
            _retryCount = 0;

            //this one should tell us that the circuit is broken
            _thirdException = await Catch.ExceptionAsync(async() => await _commandProcessor.SendAsync(new MyCommand()));
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
