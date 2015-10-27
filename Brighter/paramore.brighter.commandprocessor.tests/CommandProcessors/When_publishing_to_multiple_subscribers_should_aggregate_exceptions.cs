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
    public class When_publishing_to_multiple_subscribers_should_aggregate_exceptions
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
            registry.Register<MyEvent, MyThrowingEventHandler>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactory(container);
            container.Register<IHandleRequests<MyEvent>, MyEventHandler>("MyEventHandler");
            container.Register<IHandleRequests<MyEvent>, MyOtherEventHandler>("MyOtherHandler");
            container.Register<IHandleRequests<MyEvent>, MyThrowingEventHandler>("MyThrowingHandler");
            container.Register<ILog>(logger);

            s_commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), logger);
        };

        private Because _of = () => s_exception = Catch.Exception(() => s_commandProcessor.Publish(s_myEvent));

        private It _should_throw_an_aggregate_exception = () => s_exception.ShouldBeOfExactType(typeof(AggregateException));
        private It _should_have_an_inner_exception_from_the_handler = () => ((AggregateException)s_exception).InnerException.ShouldBeOfExactType(typeof(InvalidOperationException));
        private It _should_publish_the_command_to_the_first_event_handler = () => MyEventHandler.ShouldReceive(s_myEvent).ShouldBeTrue();
        private It _should_publish_the_command_to_the_second_event_handler = () => MyOtherEventHandler.Shouldreceive(s_myEvent).ShouldBeTrue();
    }
}