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
using TinyIoC;

namespace Paramore.Brighter.Core.Tests.ExceptionPolicy
{
    public class CommandProcessorWithRetryPolicyAsyncTests
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new MyCommand();
        private int _retryCount;

        public CommandProcessorWithRetryPolicyAsyncTests()
        {
            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyFailsWithFallbackDivideByZeroHandlerAsync>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactoryAsync(container);
            container.Register<IHandleRequestsAsync<MyCommand>, MyFailsWithFallbackDivideByZeroHandlerAsync>().AsSingleton();
            container.Register<IHandleRequestsAsync<MyCommand>, ExceptionPolicyHandlerAsync<MyCommand>>().AsSingleton();

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

            MyFailsWithFallbackDivideByZeroHandlerAsync.ReceivedCommand = false;

            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), policyRegistry);
        }

        //We have to catch the final exception that bubbles out after retry
        [Fact]
        public async Task When_Sending_A_Command_That_Should_Retry_Failure_Async()
        {
            await Catch.ExceptionAsync(() => _commandProcessor.SendAsync(_myCommand));

            //_should_send_the_command_to_the_command_handler
            MyFailsWithFallbackDivideByZeroHandlerAsync.ShouldReceive(_myCommand).Should().BeTrue();
            //_should_retry_three_times
            _retryCount.Should().Be(3);
        }
    }
}
