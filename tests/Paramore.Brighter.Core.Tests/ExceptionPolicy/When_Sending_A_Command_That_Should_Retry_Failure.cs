using System;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.ExceptionPolicy.TestDoubles;
using Paramore.Brighter.Policies.Handlers;
using Polly;
using Polly.Registry;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.Core.Tests.ExceptionPolicy
{
    public class CommandProcessorWithRetryPolicyTests
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new MyCommand();
        private int _retryCount;
        private readonly ServiceProvider _provider;
        public CommandProcessorWithRetryPolicyTests()
        {
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyFailsWithDivideByZeroHandler>();
            var container = new ServiceCollection();
            container.AddSingleton<MyFailsWithDivideByZeroHandler>();
            container.AddSingleton<ExceptionPolicyHandler<MyCommand>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });
            _provider = container.BuildServiceProvider();
            var handlerFactory = new ServiceProviderHandlerFactory(_provider);
            var policyRegistry = new PolicyRegistry();
            var policy = Policy.Handle<DivideByZeroException>().WaitAndRetry([TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(30)], (exception, timeSpan) => _retryCount++);
            policyRegistry.Add("MyDivideByZeroPolicy", policy);
            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), policyRegistry, new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
        }

        //We have to catch the final exception that bubbles out after retry
        [Test]
        public async Task When_Sending_A_Command_That_Should_Retry_Failure()
        {
            Catch.Exception(() => _commandProcessor.Send(_myCommand));
            //_should_send_the_command_to_the_command_handler
            await Assert.That(_provider.GetRequiredService<MyFailsWithDivideByZeroHandler>().ShouldReceive(_myCommand)).IsTrue();
            //_should_retry_three_times
            await Assert.That(_retryCount).IsEqualTo(3);
        }

        [After(Test)]
        public void Dispose()
        {
            _provider.Dispose();
        }
    }
}
