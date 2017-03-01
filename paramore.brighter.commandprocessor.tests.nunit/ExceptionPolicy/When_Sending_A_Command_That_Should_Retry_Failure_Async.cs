using System;
using FluentAssertions;
using Nito.AsyncEx;
using NUnit.Framework;
using paramore.brighter.commandprocessor.policy.Handlers;
using paramore.brighter.commandprocessor.tests.nunit.CommandProcessors.TestDoubles;
using paramore.brighter.commandprocessor.tests.nunit.ExceptionPolicy.TestDoubles;
using Polly;
using TinyIoC;

namespace paramore.brighter.commandprocessor.tests.nunit.ExceptionPolicy
{
    [TestFixture]
    public class CommandProcessorWithRetryPolicyAsyncTests
    {
        private static CommandProcessor s_commandProcessor;
        private static readonly MyCommand s_myCommand = new MyCommand();
        private static int s_retryCount;

        [SetUp]
        public void Establish()
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
                    s_retryCount++;
                });
            policyRegistry.Add("MyDivideByZeroPolicy", policy);

            MyFailsWithFallbackDivideByZeroHandlerAsync.ReceivedCommand = false;

            s_commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), policyRegistry);
        }

        //We have to catch the final exception that bubbles out after retry
        [Test]
        public void When_Sending_A_Command_That_Should_Retry_Failure_Async()
        {
            Catch.Exception(() => AsyncContext.Run(async () => await s_commandProcessor.SendAsync(s_myCommand)));

            //_should_send_the_command_to_the_command_handler
            MyFailsWithFallbackDivideByZeroHandlerAsync.ShouldReceive(s_myCommand).ShouldBeTrue();
            //_should_retry_three_times
            s_retryCount.ShouldEqual(3);
        }
    }
}
