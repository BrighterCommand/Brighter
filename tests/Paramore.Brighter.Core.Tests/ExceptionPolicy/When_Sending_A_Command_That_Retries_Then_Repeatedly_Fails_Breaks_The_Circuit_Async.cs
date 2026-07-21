using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Policies.Handlers;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;

namespace Paramore.Brighter.Core.Tests.ExceptionPolicy
{
    public class CommandProcessorWithBothRetryAndCircuitBreakerAsync
    {
        private readonly CommandProcessor _commandProcessor;
        private Exception? _thirdException;
        private Exception? _firstException;
        private Exception? _secondException;
        private int _retryCount;
        private Polly.Context _context;
        public CommandProcessorWithBothRetryAndCircuitBreakerAsync()
        {
            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyMultiplePoliciesFailsWithDivideByZeroHandlerAsync>();
            var container = new ServiceCollection();
            container.AddSingleton<MyMultiplePoliciesFailsWithDivideByZeroHandlerAsync>();
            container.AddSingleton<ExceptionPolicyHandlerAsync<MyCommand>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions() { HandlerLifetime = ServiceLifetime.Transient });
            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());
            var policyRegistry = new PolicyRegistry();
            var retryPolicy = Policy.Handle<DivideByZeroException>().WaitAndRetryAsync([TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(30)], (exception, timeSpan) => _retryCount++);
            var breakerPolicy = Policy.Handle<DivideByZeroException>().CircuitBreakerAsync(exceptionsAllowedBeforeBreaking: 2, durationOfBreak: TimeSpan.FromSeconds(30), onBreak: (exception, timespan, context) =>
            {
            }, onReset: context => _context = context);
            policyRegistry.Add("MyDivideByZeroRetryPolicyAsync", retryPolicy);
            policyRegistry.Add("MyDivideByZeroBreakerPolicyAsync", breakerPolicy);
            MyMultiplePoliciesFailsWithDivideByZeroHandlerAsync.ReceivedCommand = false;
            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), policyRegistry, new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
        }

        [Test]
        public async Task When_Sending_A_Command_That_Retries_Then_Repeatedly_Fails_Breaks_The_Circuit()
        {
            // First two should be caught, and increment the count
            _firstException = await Catch.ExceptionAsync(() => _commandProcessor.SendAsync(new MyCommand()));
            // Should have retried three times
            await Assert.That(_retryCount).IsEqualTo(3);
            _retryCount = 0;
            _secondException = await Catch.ExceptionAsync(() => _commandProcessor.SendAsync(new MyCommand()));
            // Should have retried three times
            await Assert.That(_retryCount).IsEqualTo(3);
            _retryCount = 0;
            // This one should tell us that the circuit is broken
            _thirdException = await Catch.ExceptionAsync(() => _commandProcessor.SendAsync(new MyCommand()));
            // Should not retry
            await Assert.That(_retryCount).IsEqualTo(0);
            // Should bubble up the first exception
            await Assert.That(_firstException).IsTypeOf<DivideByZeroException>();
            // Should bubble up the second exception
            await Assert.That(_secondException).IsTypeOf<DivideByZeroException>();
            // Should bubble up the circuit breaker exception
            await Assert.That(_thirdException).IsTypeOf<BrokenCircuitException>();
        }
    }
}