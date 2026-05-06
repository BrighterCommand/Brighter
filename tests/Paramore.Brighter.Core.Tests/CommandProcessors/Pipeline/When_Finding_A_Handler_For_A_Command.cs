using System;
using System.Collections.Generic;
using System.Linq;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline
{
    public class PipelineBuildForCommandTests
    {
        private readonly PipelineBuilder<MyCommand> _pipelineBuilder;
        private IHandleRequests<MyCommand>? _pipeline;
        public PipelineBuildForCommandTests()
        {
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyCommandHandler>();
            var handlerFactory = new SimpleHandlerFactorySync(_ => new MyCommandHandler(new Dictionary<string, string>()));
            _pipelineBuilder = new PipelineBuilder<MyCommand>(registry, handlerFactory);
        }

        [Test]
        public async Task When_Finding_A_Handler_For_A_Command()
        {
            _pipeline = _pipelineBuilder.Build(new MyCommand(), new RequestContext()).First();
            await Assert.That(_pipeline).IsTypeOf<MyCommandHandler>();
            await Assert.That(TracePipeline().ToString()).IsEqualTo("MyCommandHandler|");
        }

        private PipelineTracer TracePipeline()
        {
            var pipelineTracer = new PipelineTracer();
            _pipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }
    }
}