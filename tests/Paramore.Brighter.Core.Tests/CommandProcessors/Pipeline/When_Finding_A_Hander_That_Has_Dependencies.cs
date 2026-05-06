using System;
using System.Linq;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline
{
    public class PipelineWithHandlerDependenciesTests
    {
        private readonly PipelineBuilder<MyCommand> _pipelineBuilder;
        private IHandleRequests<MyCommand> _pipeline;
        public PipelineWithHandlerDependenciesTests()
        {
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyDependentCommandHandler>();
            var handlerFactory = new SimpleHandlerFactorySync(_ => new MyDependentCommandHandler(new FakeRepository<MyAggregate>(new FakeSession())));
            _pipelineBuilder = new PipelineBuilder<MyCommand>(registry, handlerFactory);
        }

        [Test]
        public async Task When_Finding_A_Handler_That_Has_Dependencies()
        {
            _pipeline = _pipelineBuilder.Build(new MyCommand(), new RequestContext()).First();
            await Assert.That(_pipeline).IsTypeOf<MyDependentCommandHandler>();
            await Assert.That(TracePipeline().ToString()).IsEqualTo("MyDependentCommandHandler|");
        }

        private PipelineTracer TracePipeline()
        {
            var pipelineTracer = new PipelineTracer();
            _pipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }
    }
}