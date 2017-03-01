using System.Linq;
using NUnit.Framework;
using paramore.brighter.commandprocessor.tests.nunit.CommandProcessors.TestDoubles;
using TinyIoC;

namespace paramore.brighter.commandprocessor.tests.nunit.CommandProcessors
{
    [TestFixture]
    public class PipelinePreAndPostFiltersAsyncTests
    {
        private PipelineBuilder<MyCommand> _pipelineBuilder;
        private IHandleRequestsAsync<MyCommand> _pipeline;

        [SetUp]
        public void Establish()
        {
            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyPreAndPostDecoratedHandlerAsync>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactoryAsync(container);
            container.Register<IHandleRequestsAsync<MyCommand>, MyPreAndPostDecoratedHandlerAsync>();
            container.Register<IHandleRequestsAsync<MyCommand>, MyValidationHandlerAsync<MyCommand>>();
            container.Register<IHandleRequestsAsync<MyCommand>, MyLoggingHandlerAsync<MyCommand>>();

            _pipelineBuilder = new PipelineBuilder<MyCommand>(registry, handlerFactory);
        }

        private void When_Building_An_Async_Pipeline_Allow_Pre_And_Post_Tasks()
        {
            _pipeline = _pipelineBuilder.BuildAsync(new RequestContext(), false).First();

            TraceFilters().ToString().ShouldEqual("MyValidationHandlerAsync`1|MyPreAndPostDecoratedHandlerAsync|MyLoggingHandlerAsync`1|");
        }

        private PipelineTracer TraceFilters()
        {
            var pipelineTracer = new PipelineTracer();
            _pipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }
    }
}
