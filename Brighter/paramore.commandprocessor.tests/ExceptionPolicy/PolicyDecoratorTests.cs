using System;
using FluentAssertions;
using Machine.Specifications;
using Polly;
using TinyIoC;
using paramore.commandprocessor.ioccontainers.IoCContainers;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;
using paramore.commandprocessor.tests.ExceptionPolicy.TestDoubles;

namespace paramore.commandprocessor.tests.ExceptionPolicy
{
   [Subject("Basic send of a command")]
    public class When_sending_a_command_to_the_processor_with_a_policy_check
    {
        static CommandProcessor commandProcessor;
        static readonly MyCommand myCommand = new MyCommand();
       static int retryCount;

        Establish context = () =>
        {
            var container = new TinyIoCAdapter(new TinyIoCContainer());
            //Handler is decorated with UsePolicy and fails with divide by zero error
            container.Register<IHandleRequests<MyCommand>, MyFailsWithDivideByZeroHandler >().AsMultiInstance();
            var policy = Policy
                .Handle<DivideByZeroException>()
                .WaitAndRetry(new[]
                {
                    1.Seconds(),
                    2.Seconds(),
                    3.Seconds()
                }, (exception, timeSpan) => 
                {
                    retryCount++;
                });
            container.Register<Policy, policy>("MyDivideByZeroPolicy");

            commandProcessor = new CommandProcessor(container, new InMemoryRequestContextFactory());

        };

        Because of = () => commandProcessor.Send(myCommand);

       It should_send_the_command_to_the_command_handler = () => MyFailsWithDivideByZeroHandler.ShouldRecieve(myCommand).ShouldBeTrue();
       It should_retry_three_times = () => retryCount.ShouldEqual(3);
    }
}
