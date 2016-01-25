using System;
using FakeItEasy;
using FluentAssertions;
using Machine.Specifications;
using Nito.AsyncEx;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.policy.Handlers;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;
using paramore.commandprocessor.tests.ExceptionPolicy.TestDoubles;
using Polly;
using TinyIoC;

namespace paramore.commandprocessor.tests.ExceptionPolicy
{
    [Subject(typeof(ExceptionPolicyHandler<>))]
    public class When_Sending_A_Command_That_Should_Retry_Failure_Async
    {
        private static CommandProcessor s_commandProcessor;
        private static readonly MyCommand s_myCommand = new MyCommand();
        private static int s_retryCount;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();

            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyFailsWithFallbackDivideByZeroHandlerAsync>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactoryAsync(container);
            container.Register<IHandleRequestsAsync<MyCommand>, MyFailsWithFallbackDivideByZeroHandlerAsync>().AsSingleton();
            container.Register<IHandleRequestsAsync<MyCommand>, ExceptionPolicyHandlerRequestHandlerAsync<MyCommand>>().AsSingleton();
            container.Register<ILog>(logger);

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

            s_commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), policyRegistry, logger);
        };

        //We have to catch the final exception that bubbles out after retry
        private Because _of = () => Catch.Exception(() => AsyncContext.Run(async () => await s_commandProcessor.SendAsync(s_myCommand)));

        private It _should_send_the_command_to_the_command_handler = () => MyFailsWithFallbackDivideByZeroHandlerAsync.ShouldReceive(s_myCommand).ShouldBeTrue();
        private It _should_retry_three_times = () => s_retryCount.ShouldEqual(3);
    }
}
