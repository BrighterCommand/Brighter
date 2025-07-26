using System;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.ExceptionPolicy.TestDoubles;
using Xunit;
using Paramore.Brighter.Policies.Handlers;
using Polly;
using Polly.Registry;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.Core.Tests.ExceptionPolicy
{
    public class CommandProcessorWithExceptionPolicyNothingThrowTests : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new MyCommand();
        private int _retryCount;

        public CommandProcessorWithExceptionPolicyNothingThrowTests()
        {
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyDoesNotFailPolicyHandler>();

            var container = new ServiceCollection();
            container.AddTransient<MyDoesNotFailPolicyHandler>();
            container.AddTransient<ExceptionPolicyHandler<MyCommand>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions {HandlerLifetime = ServiceLifetime.Transient});


            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());
            
            var policyRegistry = new PolicyRegistry();

            var policy = Policy
                .Handle<DivideByZeroException>()
                .WaitAndRetry([
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(3)
                ], (exception, timeSpan) =>
                {
                    _retryCount++;
                });
            policyRegistry.Add("MyDivideByZeroPolicy", policy);

            MyDoesNotFailPolicyHandler.ReceivedCommand = false;

            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), policyRegistry, new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
        }

        //We have to catch the final exception that bubbles out after retry
        [Fact]
        public void When_Sending_A_Command_That_Passes_Policy_Check()
        {
            _commandProcessor.Send(_myCommand);

            // Should send the command to the command handler
            Assert.True(MyDoesNotFailPolicyHandler.Shouldreceive(_myCommand));
            // Should not retry
            Assert.Equal(0, _retryCount);
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
