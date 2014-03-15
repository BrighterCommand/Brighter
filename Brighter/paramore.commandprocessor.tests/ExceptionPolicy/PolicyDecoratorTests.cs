using System;
using FluentAssertions;
using Machine.Specifications;
using Polly;
using Polly.CircuitBreaker;
using TinyIoC;
using paramore.commandprocessor.ioccontainers.IoCContainers;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;
using paramore.commandprocessor.tests.ExceptionPolicy.TestDoubles;

namespace paramore.commandprocessor.tests.ExceptionPolicy
{
   [Subject("Basic policy on a handler")]
    public class When_sending_a_command_to_the_processor_with_a_retry_policy_check
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
            container.Register<Policy>("MyDivideByZeroPolicy", policy);

            MyFailsWithDivideByZeroHandler.ReceivedCommand = false;

            commandProcessor = new CommandProcessor(container, new InMemoryRequestContextFactory());

        };

       //We have to catch the final exception that bubbles out after retry
        Because of = () => Catch.Exception(() => commandProcessor.Send(myCommand));

       It should_send_the_command_to_the_command_handler = () => MyFailsWithDivideByZeroHandler.ShouldRecieve(myCommand).ShouldBeTrue();
       It should_retry_three_times = () => retryCount.ShouldEqual(3);
    }

    [Subject("Basic policy on a handler")]
    public class When_sending_a_command_to_the_processor_passes_policy_check
    {
        static CommandProcessor commandProcessor;
        static readonly MyCommand myCommand = new MyCommand();
        static int retryCount;

        Establish context = () =>
        {
            var container = new TinyIoCAdapter(new TinyIoCContainer());
            //Handler is decorated with UsePolicy and fails with divide by zero error
            container.Register<IHandleRequests<MyCommand>, MyDoesNotFailPolicyHandler >().AsMultiInstance();
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
            container.Register<Policy>("MyDivideByZeroPolicy", policy);

            MyDoesNotFailPolicyHandler.ReceivedCommand = false;

            commandProcessor = new CommandProcessor(container, new InMemoryRequestContextFactory());

        };

       //We have to catch the final exception that bubbles out after retry
        Because of = () => commandProcessor.Send(myCommand);

       It should_send_the_command_to_the_command_handler = () => MyDoesNotFailPolicyHandler .ShouldRecieve(myCommand).ShouldBeTrue();
       It should_not_retry = () => retryCount.ShouldEqual(0);
    }

    [Subject("Basic policy on a handler")]
    public class When_sending_a_command_to_the_processor_with_a_circuit_breaker
    {
        static CommandProcessor commandProcessor;
        static readonly MyCommand myCommand = new MyCommand();
        static Exception thirdException;
        static Exception firstException;
        static Exception secondException;

        Establish context = () =>
        {
            var container = new TinyIoCAdapter(new TinyIoCContainer());
            //Handler is decorated with UsePolicy and fails with divide by zero error
            container.Register<IHandleRequests<MyCommand>, MyFailsWithDivideByZeroHandler >().AsMultiInstance();
            var policy = Policy
                .Handle<DivideByZeroException>()
                .CircuitBreaker(2, TimeSpan.FromMinutes(1));

            container.Register<Policy>("MyDivideByZeroPolicy", policy);

            MyFailsWithDivideByZeroHandler.ReceivedCommand = false;

            commandProcessor = new CommandProcessor(container, new InMemoryRequestContextFactory());

        };

       //We have to catch the final exception that bubbles out after retry
        Because of = () =>
            {
                //First two should be caught, and increment the count
                firstException = Catch.Exception(() => commandProcessor.Send(myCommand));
                secondException = Catch.Exception(() => commandProcessor.Send(myCommand));
                //this one should tell us that the circuit ir broken
                thirdException = Catch.Exception(() => commandProcessor.Send(myCommand));
            };

       It should_send_the_command_to_the_command_handler = () => MyFailsWithDivideByZeroHandler.ShouldRecieve(myCommand).ShouldBeTrue();
       It should_bubble_up_the_first_exception = () => firstException.ShouldBeOfExactType<DivideByZeroException>();
       It should_bubble_up_the_second_exception = () => secondException.ShouldBeOfExactType<DivideByZeroException>(); 
       It should_break_the_circuit_after_two_fails = () => thirdException.ShouldBeOfExactType<BrokenCircuitException>();
    }
}
