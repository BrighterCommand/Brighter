using System;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.ExceptionPolicy.TestDoubles;
using Xunit;
using Paramore.Brighter.Policies.Handlers;
using Polly;
using Polly.Registry;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.Core.Tests.ExceptionPolicy
{
    public class CommandProcessorWithRetryPolicyTests : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new MyCommand();
        private int _retryCount;

        public CommandProcessorWithRetryPolicyTests()
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
                .WaitAndRetry([
                        TimeSpan.FromMilliseconds(10), 
                        TimeSpan.FromMilliseconds(20), 
                        TimeSpan.FromMilliseconds(30)
                    ],
                    (exception, timeSpan) =>
                        _retryCount++
                );
            
            policyRegistry.Add("MyDivideByZeroPolicy", policy);

            MyFailsWithDivideByZeroHandler.ReceivedCommand = false;

            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), policyRegistry, new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
        }

        //We have to catch the final exception that bubbles out after retry
        [Fact]
        public void When_Sending_A_Command_That_Should_Retry_Failure()
        {
            Catch.Exception(() => _commandProcessor.Send(_myCommand));

            //_should_send_the_command_to_the_command_handler
            Assert.True(MyFailsWithDivideByZeroHandler.ShouldReceive(_myCommand));
            //_should_retry_three_times
            Assert.Equal(3, _retryCount);
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
