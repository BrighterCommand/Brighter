using System;
using System.Linq;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline
{
    [Collection("CommandProcessor")]
    public class PipelineWithHandlerDependenciesTests : IDisposable

    {
    private readonly PipelineBuilder<MyCommand> _pipelineBuilder;
    private IHandleRequests<MyCommand> _pipeline;

    public PipelineWithHandlerDependenciesTests()
    {
        var registry = new SubscriberRegistry();
        registry.Register<MyCommand, MyDependentCommandHandler>();
        var handlerFactory =
            new SimpleHandlerFactorySync(_ => new MyDependentCommandHandler(new FakeRepository<MyAggregate>(new FakeSession())));

        _pipelineBuilder = new PipelineBuilder<MyCommand>(registry, handlerFactory);
        PipelineBuilder<MyCommand>.ClearPipelineCache();
    }

    [Fact]
    public void When_Finding_A_Handler_That_Has_Dependencies()
    {
        _pipeline = _pipelineBuilder.Build(new MyCommand(), new RequestContext()).First();

        Assert.IsType<MyDependentCommandHandler>(_pipeline);
        Assert.Equal("MyDependentCommandHandler|", TracePipeline().ToString());
    }

    public void Dispose()
    {
       CommandProcessor.ClearServiceBus(); 
    }

    private PipelineTracer TracePipeline()
    {
        var pipelineTracer = new PipelineTracer();
        _pipeline.DescribePath(pipelineTracer);
        return pipelineTracer;
    }
    }
}
