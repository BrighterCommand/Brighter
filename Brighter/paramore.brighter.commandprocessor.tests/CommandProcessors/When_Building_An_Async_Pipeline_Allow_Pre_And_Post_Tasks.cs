using System.Linq;
using FakeItEasy;
using Machine.Specifications;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;
using TinyIoC;

namespace paramore.commandprocessor.tests.CommandProcessors
{
    [Subject(typeof(PipelineBuilder<>))]
    public class When_Building_An_Async_Pipeline_Allow_Pre_And_Post_Tasks
    {
        private static PipelineBuilder<MyCommand> s_pipeline_Builder;
        private static IHandleRequestsAsync<MyCommand> s_pipeline;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();

            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyPreAndPostDecoratedHandlerRequestHandlerAsync>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactoryAsync(container);
            container.Register<IHandleRequestsAsync<MyCommand>, MyPreAndPostDecoratedHandlerRequestHandlerAsync>();
            container.Register<IHandleRequestsAsync<MyCommand>, MyValidationHandlerRequestHandlerAsync<MyCommand>>();
            container.Register<IHandleRequestsAsync<MyCommand>, MyLoggingHandlerRequestHandlerAsync<MyCommand>>();
            container.Register<ILog>(logger);

            s_pipeline_Builder = new PipelineBuilder<MyCommand>(registry, handlerFactory, logger);
        };

        private Because _of = () => s_pipeline = s_pipeline_Builder.BuildAsync(new RequestContext(), false).First();

        private It _should_add_handlers_in_the_correct_sequence_into_the_chain = () => TraceFilters().ToString().ShouldEqual("MyValidationHandlerAsync`1|MyPreAndPostDecoratedHandlerAsync|MyLoggingHandlerAsync`1|");

        private static PipelineTracer TraceFilters()
        {
            var pipelineTracer = new PipelineTracer();
            s_pipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }
    }
}
