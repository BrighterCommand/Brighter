using System.Linq;
using NUnit.Framework;
using Paramore.Brighter.Tests.TestDoubles;
using TinyIoC;

namespace Paramore.Brighter.Tests
{
    [TestFixture]
    public class PipelineForiegnAttributesAsyncTests
    {
        private PipelineBuilder<MyCommand> _pipeline_Builder;
        private IHandleRequestsAsync<MyCommand> _pipeline;

        [SetUp]
        public void Establish()
        {
            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyObsoleteCommandHandlerAsync>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactoryAsync(container);
            container.Register<IHandleRequestsAsync<MyCommand>, MyObsoleteCommandHandlerAsync>();
            container.Register<IHandleRequestsAsync<MyCommand>, MyValidationHandlerAsync<MyCommand>>();
            container.Register<IHandleRequestsAsync<MyCommand>, MyLoggingHandlerAsync<MyCommand>>();

            _pipeline_Builder = new PipelineBuilder<MyCommand>(registry, handlerFactory);
        }

        public void When_Building_An_Async_Pipeline_Allow_ForiegnAttribues()
        {
            _pipeline = _pipeline_Builder.BuildAsync(new RequestContext(), false).First();

            Assert.AreEqual("MyValidationHandlerAsync`1|MyObsoleteCommandHandlerAsync|MyLoggingHandlerAsync`1|", TraceFilters().ToString());
        }

        private PipelineTracer TraceFilters()
        {
            var pipelineTracer = new PipelineTracer();
            _pipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }
    }
}
