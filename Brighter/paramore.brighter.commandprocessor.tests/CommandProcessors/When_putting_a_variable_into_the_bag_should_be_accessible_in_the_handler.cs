using FakeItEasy;
using Machine.Specifications;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;

namespace paramore.commandprocessor.tests.CommandProcessors
{
    [Subject(typeof(PipelineBuilder<>))]
    public class When_putting_a_variable_into_the_bag_should_be_accessible_in_the_handler
    {
        private const string I_AM_A_TEST_OF_THE_CONTEXT_BAG = "I am a test of the context bag";
        private static RequestContext s_request_context;
        private static CommandProcessor s_commandProcessor;
        private static MyCommand s_myCommand;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();

            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyContextAwareCommandHandler>();
            var handlerFactory = new TestHandlerFactory<MyCommand, MyContextAwareCommandHandler>(() => new MyContextAwareCommandHandler());
            s_request_context = new RequestContext();
            s_myCommand = new MyCommand();
            MyContextAwareCommandHandler.TestString = null;

            var requestContextFactory = A.Fake<IAmARequestContextFactory>();
            A.CallTo(() => requestContextFactory.Create()).Returns(s_request_context);

            s_commandProcessor = new CommandProcessor(registry, handlerFactory, requestContextFactory, new PolicyRegistry(), logger);

            s_request_context.Bag["TestString"] = I_AM_A_TEST_OF_THE_CONTEXT_BAG;
        };


        private Because _of = () => s_commandProcessor.Send(s_myCommand);

        private It _should_have_seen_the_data_we_pushed_into_the_bag = () => MyContextAwareCommandHandler.TestString.ShouldEqual(I_AM_A_TEST_OF_THE_CONTEXT_BAG);
        private It _should_have_been_filled_by_the_handler = () => ((string)s_request_context.Bag["MyContextAwareCommandHandler"]).ShouldEqual("I was called and set the context");
    }
}