using System;
using FakeItEasy;
using nUnitShouldAdapter;
using NUnit.Specifications;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.tests.nunit.CommandProcessors.TestDoubles;
using TinyIoC;

namespace paramore.brighter.commandprocessor.tests.nunit.CommandProcessors
{
    public class When_An_Exception_Is_Thrown_Terminate_The_Pipeline : NUnit.Specifications.ContextSpecification
    {
        private static CommandProcessor s_commandProcessor;
        private static readonly MyCommand s_myCommand = new MyCommand();
        private static Exception s_exception;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();

            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyUnusedCommandHandler>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactory(container);
            container.Register<IHandleRequests<MyCommand>, MyUnusedCommandHandler>();
            container.Register<IHandleRequests<MyCommand>, MyAbortingHandler<MyCommand>>();
            container.Register<ILog>(logger);

            s_commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), logger);
        };

        private Because _of = () => s_exception = Catch.Exception(() => s_commandProcessor.Send(s_myCommand));

        private Cleanup cleanup = () => s_commandProcessor.Dispose();

        private It _should_throw_an_exception = () => s_exception.ShouldNotBeNull();
        private It _should_fail_the_pipeline_not_execute_it = () => MyUnusedCommandHandler.Shouldreceive(s_myCommand).ShouldBeFalse();
    }
}