using System.Linq;
using FakeItEasy;
using Machine.Specifications;
using TinyIoC;
using paramore.commandprocessor.ioccontainers.IoCContainers;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;

namespace paramore.commandprocessor.tests.CommandProcessors
{
    [Subject(typeof(PipelineBuilder<>))]
    public class When_Building_A_Handler_For_A_Command
    {
        private static PipelineBuilder<MyCommand> Chain_Builder;
        private static IHandleRequests<MyCommand> Chain_Of_Responsibility;
        private static readonly RequestContext request_context = new RequestContext();

        Establish context = () =>
        {
            var container = new TinyIoCAdapter(new TinyIoCContainer());
            container.Register<IHandleRequests<MyCommand>, MyCommandHandler>().AsMultiInstance();

            Chain_Builder = new PipelineBuilder<MyCommand>(container);
        };

        Because of = () => Chain_Of_Responsibility = Chain_Builder.Build(request_context).First();

        It should_have_set_the_context_on_the_handler = () => Chain_Of_Responsibility.Context.ShouldNotBeNull();
        It should_use_the_context_that_we_passed_in = () => Chain_Of_Responsibility.Context.ShouldBeTheSameAs(request_context);
    }

    [Subject(typeof(PipelineBuilder<>))]
    public class When_putting_a_variable_into_the_bag_should_be_accessible_in_the_handler
    {
        private const string I_AM_A_TEST_OF_THE_CONTEXT_BAG = "I am a test of the context bag";
        private static readonly RequestContext request_context = new RequestContext();
        private static CommandProcessor commandProcessor;
        private static MyCommand myCommand;

        Establish context = () =>
        {
            var container = new TinyIoCAdapter(new TinyIoCContainer());
            container.Register<IHandleRequests<MyCommand>, MyContextAwareCommandHandler>().AsMultiInstance();

            myCommand = new MyCommand();
            MyContextAwareCommandHandler.TestString = null;

            var requestContextFactory = A.Fake<IAmARequestContextFactory>();
            A.CallTo(() => requestContextFactory.Create()).Returns(request_context);

            commandProcessor = new CommandProcessor(container, requestContextFactory);

            request_context.Bag["TestString"] = I_AM_A_TEST_OF_THE_CONTEXT_BAG;
        };


        Because of = () => commandProcessor.Send(myCommand);

        It should_have_seen_the_data_we_pushed_into_the_bag = () => MyContextAwareCommandHandler.TestString.ShouldEqual(I_AM_A_TEST_OF_THE_CONTEXT_BAG);
        It should_have_been_filled_by_the_handler = () => ((string)request_context.Bag["MyContextAwareCommandHandler"]).ShouldEqual("I was called and set the context");
    }


}
