using System;
using FakeItEasy;
using Machine.Specifications;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;
using TinyIoC;

namespace paramore.commandprocessor.tests.CommandProcessors
{
    [Subject("An event with multiple subscribers")]
    public class When_There_Are_Multiple_Subscribers
    {
        private static CommandProcessor s_commandProcessor;
        private static readonly MyEvent s_myEvent = new MyEvent();
        private static Exception s_exception;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();

            var registry = new SubscriberRegistry();
            registry.Register<MyEvent, MyEventHandler>();
            registry.Register<MyEvent, MyOtherEventHandler>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactory(container);
            container.Register<IHandleRequests<MyEvent>, MyEventHandler>("MyEventHandler");
            container.Register<IHandleRequests<MyEvent>, MyOtherEventHandler>("MyOtherHandler");
            container.Register<ILog>(logger);

            s_commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), logger);
        };

        private Because _of = () => s_exception = Catch.Exception(() => s_commandProcessor.Publish(s_myEvent));

        private It _should_not_throw_an_exception = () => s_exception.ShouldBeNull();
        private It _should_publish_the_command_to_the_first_event_handler = () => MyEventHandler.ShouldReceive(s_myEvent).ShouldBeTrue();
        private It _should_publish_the_command_to_the_second_event_handler = () => MyOtherEventHandler.Shouldreceive(s_myEvent).ShouldBeTrue();
    }
}