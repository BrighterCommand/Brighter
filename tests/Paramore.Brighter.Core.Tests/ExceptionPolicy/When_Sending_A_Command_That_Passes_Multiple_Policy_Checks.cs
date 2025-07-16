using System;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.ExceptionPolicy.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Policies.Handlers;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests
{
    public class CommandProcessorWithMultipleExceptionPoliciesNothingThrowTests : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new MyCommand();
        private int _retryCount;

        public CommandProcessorWithMultipleExceptionPoliciesNothingThrowTests()
        {
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyDoesNotFailMultiplePoliciesHandler>();

            var container = new ServiceCollection();
            container.AddTransient<MyDoesNotFailMultiplePoliciesHandler>();
            container.AddTransient<ExceptionPolicyHandler<MyCommand>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions()
            {
                HandlerLifetime = ServiceLifetime.Transient
            });


            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());

            var policyRegistry = new PolicyRegistry();

            var retryPolicy = Policy
                .Handle<DivideByZeroException>()
                .WaitAndRetry([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3)], (exception, timeSpan) =>
                {
                    _retryCount++;
                });

            var breakerPolicy = Policy.Handle<DivideByZeroException>()
                .CircuitBreaker(1, TimeSpan.FromMilliseconds(500));

            policyRegistry.Add("MyDivideByZeroRetryPolicy", retryPolicy);
            policyRegistry.Add("MyDivideByZeroBreakerPolicy", breakerPolicy);

            MyDoesNotFailMultiplePoliciesHandler.ReceivedCommand = false;

            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(),
                policyRegistry, new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());

        }

        [Fact]
        public void When_Sending_A_Command_That_Passes_Multiple_Policy_Checks()
        {
            _commandProcessor.Send(_myCommand);

            // Should send the command to the command handler
            Assert.True(MyDoesNotFailMultiplePoliciesHandler.ShouldReceive(_myCommand));
            // Should not retry
            Assert.Equal(0, _retryCount);
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
