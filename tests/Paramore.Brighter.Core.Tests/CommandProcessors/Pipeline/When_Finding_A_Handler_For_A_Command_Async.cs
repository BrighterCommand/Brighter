using System;
using System.Collections.Generic;
using System.Linq;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline
{
    public class PipelineBuildForCommandAsyncTests
    {
        private readonly PipelineBuilder<MyCommand> _pipelineBuilder;
        private IHandleRequestsAsync<MyCommand> _pipeline;
        public PipelineBuildForCommandAsyncTests()
        {
            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyCommandHandlerAsync>();
            var handlerFactory = new SimpleHandlerFactoryAsync(_ => new MyCommandHandlerAsync(new Dictionary<string, string>()));
            _pipelineBuilder = new PipelineBuilder<MyCommand>(registry, handlerFactory);
        }

        [Test]
        public async Task When_Finding_A_Handler_For_A_Command()
        {
            _pipeline = _pipelineBuilder.BuildAsync(new MyCommand(), new RequestContext(), true).First();
            await Assert.That(_pipeline).IsTypeOf<MyCommandHandlerAsync>();
            await Assert.That(TracePipeline().ToString()).IsEqualTo("MyCommandHandlerAsync|");
        }

        private PipelineTracer TracePipeline()
        {
            var pipelineTracer = new PipelineTracer();
            _pipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }
    }
}