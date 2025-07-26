using System;
using System.Threading.Tasks;
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
    public class CommandProcessorWithRetryPolicyAsyncTests : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new MyCommand();
        private int _retryCount;
        private ServiceProvider _provider;

        public CommandProcessorWithRetryPolicyAsyncTests()
        {
            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyFailsWithFallbackDivideByZeroHandlerAsync>();

            var container = new ServiceCollection();
            container.AddSingleton<MyFailsWithFallbackDivideByZeroHandlerAsync>();
            container.AddSingleton<ExceptionPolicyHandlerAsync<MyCommand>>();
            container.AddSingleton<FallbackPolicyHandlerRequestHandlerAsync<MyCommand>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions {HandlerLifetime = ServiceLifetime.Transient});

            _provider = container.BuildServiceProvider();
            var handlerFactory = new ServiceProviderHandlerFactory(_provider);

            var policyRegistry = new PolicyRegistry();

            var policy = Policy
                .Handle<DivideByZeroException>()
                .WaitAndRetryAsync([
                        TimeSpan.FromMilliseconds(10), 
                        TimeSpan.FromMilliseconds(20), 
                        TimeSpan.FromMilliseconds(30)
                    ],
                    (exception, timeSpan) =>
                        _retryCount++
                );
            
            policyRegistry.Add("MyDivideByZeroPolicy", policy);

            _provider.GetService<MyFailsWithFallbackDivideByZeroHandlerAsync>().ReceivedCommand = false;
            
            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), policyRegistry, new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
        }

        //We have to catch the final exception that bubbles out after retry
        [Fact]
        public async Task When_Sending_A_Command_That_Should_Retry_Failure_Async()
        {
            await Catch.ExceptionAsync(async () => await _commandProcessor.SendAsync(_myCommand));

            //_should_send_the_command_to_the_command_handler
            var zeroHandlerAsync = _provider.GetService<MyFailsWithFallbackDivideByZeroHandlerAsync>();
            Assert.True(zeroHandlerAsync!.ShouldReceive(_myCommand));
            //_should_retry_three_times
            Assert.Equal(3, _retryCount);
        }

        public void Dispose()
        {
            _provider?.Dispose();
            CommandProcessor.ClearServiceBus();
        }
    }
}
