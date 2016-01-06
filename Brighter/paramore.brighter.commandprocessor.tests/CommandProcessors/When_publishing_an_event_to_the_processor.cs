using FakeItEasy;
using Machine.Specifications;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;

namespace paramore.commandprocessor.tests.CommandProcessors
{
    [Subject("Basic event publishing")]
    public class When_Publishing_An_Event_To_The_Processor
    {
        private static CommandProcessor s_commandProcessor;
        private static readonly MyEvent s_myEvent = new MyEvent();

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();

            var registry = new SubscriberRegistry();
            registry.Register<MyEvent, MyEventHandler>();
            var handlerFactory = new TestHandlerFactory<MyEvent, MyEventHandler>(() => new MyEventHandler(logger));

            s_commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), logger);
        };

        private Because _of = () => s_commandProcessor.Publish(s_myEvent);
        private It _should_publish_the_command_to_the_event_handlers = () => MyEventHandler.ShouldReceive(s_myEvent).ShouldBeTrue();
    }
}