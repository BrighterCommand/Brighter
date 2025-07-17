using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline
{
    [Collection("CommandProcessor")]
    public class PipelineForiegnAttributesAsyncTests :IDisposable

    {
    private readonly PipelineBuilder<MyCommand> _pipeline_Builder;
    private IHandleRequestsAsync<MyCommand> _pipeline;

    public PipelineForiegnAttributesAsyncTests()
    {
        var registry = new SubscriberRegistry();
        registry.RegisterAsync<MyCommand, MyObsoleteCommandHandlerAsync>();

        var container = new ServiceCollection();

        container.AddTransient<MyObsoleteCommandHandlerAsync>();
        container.AddTransient<MyValidationHandlerAsync<MyCommand>>();
        container.AddTransient<MyLoggingHandlerAsync<MyCommand>>();
        container.AddSingleton<IBrighterOptions>(new BrighterOptions {HandlerLifetime = ServiceLifetime.Transient});
 
        var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());

        _pipeline_Builder = new PipelineBuilder<MyCommand>(registry, (IAmAHandlerFactoryAsync)handlerFactory);
        PipelineBuilder<MyCommand>.ClearPipelineCache();
    }

    [Fact]
    public void When_Building_An_Async_Pipeline_Allow_ForeignAttributes()
    {
        _pipeline = _pipeline_Builder.BuildAsync(new MyCommand(), new RequestContext(), false).First();

        var trace = TraceFilters().ToString();
        Assert.Equal("MyValidationHandlerAsync`1|MyObsoleteCommandHandlerAsync|MyLoggingHandlerAsync`1|", trace);
    }

    public void Dispose()
    {
        CommandProcessor.ClearServiceBus();
    }

    private PipelineTracer TraceFilters()
    {
        var pipelineTracer = new PipelineTracer();
        _pipeline.DescribePath(pipelineTracer);
        return pipelineTracer;
    }
    }
}
