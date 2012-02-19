using System.Linq;
using Machine.Specifications;
using TinyIoC;
using paramore.commandprocessor.ioccontainers.IoCContainers;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;

namespace paramore.commandprocessor.tests.CommandProcessors
{
    [Subject(typeof(ChainofResponsibilityBuilder<>))]
    public class When_Building_A_Handler_For_A_Command
    {
        private static ChainofResponsibilityBuilder<MyCommand> Chain_Builder;
        private static IHandleRequests<MyCommand> Chain_Of_Responsibility;
        private static readonly RequestContext request_context = new RequestContext();

        Establish context = () =>
        {
            var container = new TinyIoCAdapter(new TinyIoCContainer());
            container.Register<IHandleRequests<MyCommand>, MyCommandHandler>().AsMultiInstance();

            Chain_Builder = new ChainofResponsibilityBuilder<MyCommand>(container);
        };

        Because of = () => Chain_Of_Responsibility = Chain_Builder.Build(request_context).First();

        It should_have_set_the_context_on_the_handler = () => Chain_Of_Responsibility.Context.ShouldNotBeNull();
        It should_use_the_context_that_we_passed_in = () => Chain_Of_Responsibility.Context.ShouldBeTheSameAs(request_context);
    }
}
