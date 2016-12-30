using System.Linq;
using nUnitShouldAdapter;
using NUnit.Specifications;
using paramore.brighter.commandprocessor.tests.nunit.CommandProcessors.TestDoubles;

namespace paramore.brighter.commandprocessor.tests.nunit.CommandProcessors
{
    [Subject(typeof(PipelineBuilder<>))]
    public class When_Finding_A_Hander_That_Has_Dependencies : ContextSpecification
    {
        private static PipelineBuilder<MyCommand> s_pipelineBuilder;
        private static IHandleRequests<MyCommand> s_pipeline;

        private Establish _context = () =>
        {
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyDependentCommandHandler>();
            var handlerFactory = new TestHandlerFactory<MyCommand, MyDependentCommandHandler>(() => new MyDependentCommandHandler(new FakeRepository<MyAggregate>(new FakeSession())));

            s_pipelineBuilder = new PipelineBuilder<MyCommand>(registry, handlerFactory);
        };

        private Because _of = () => s_pipeline = s_pipelineBuilder.Build(new RequestContext()).First();

        private It _should_return_the_command_handler_as_the_implicit_handler = () => s_pipeline.ShouldBeAssignableTo(typeof(MyDependentCommandHandler));
        private It _should_be_the_only_element_in_the_chain = () => TracePipeline().ToString().ShouldEqual("MyDependentCommandHandler|");

        private static PipelineTracer TracePipeline()
        {
            var pipelineTracer = new PipelineTracer();
            s_pipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }
    }
}