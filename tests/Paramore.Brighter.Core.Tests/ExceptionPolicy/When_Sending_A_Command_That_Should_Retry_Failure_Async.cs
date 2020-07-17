using System;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Extensions;
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
    public class CommandProcessorWithRetryPolicyAsyncTests
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

            _provider = container.BuildServiceProvider();
            var handlerFactory = new ServiceProviderHandlerFactory(_provider);

            var policyRegistry = new PolicyRegistry();

            var policy = Policy
                .Handle<DivideByZeroException>()
                .WaitAndRetryAsync(new[]
                {
                    1.Seconds(),
                    2.Seconds(),
                    3.Seconds()
                }, (exception, timeSpan) =>
                {
                    _retryCount++;
                });
            policyRegistry.Add("MyDivideByZeroPolicy", policy);

            _provider.GetService<MyFailsWithFallbackDivideByZeroHandlerAsync>().ReceivedCommand = false;
            
            _commandProcessor = new CommandProcessor(registry, (IAmAHandlerFactoryAsync)handlerFactory, new InMemoryRequestContextFactory(), policyRegistry);
        }

        //We have to catch the final exception that bubbles out after retry
        [Fact]
        public async Task When_Sending_A_Command_That_Should_Retry_Failure_Async()
        {
            await Catch.ExceptionAsync(() => _commandProcessor.SendAsync(_myCommand));

            //_should_send_the_command_to_the_command_handler
            _provider.GetService<MyFailsWithFallbackDivideByZeroHandlerAsync>().ShouldReceive(_myCommand).Should().BeTrue();
            //_should_retry_three_times
            _retryCount.Should().Be(3);
        }
    }
}
