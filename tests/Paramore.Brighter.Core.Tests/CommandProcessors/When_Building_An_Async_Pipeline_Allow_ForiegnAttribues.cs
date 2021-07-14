using System;
using System.Linq;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors
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

        var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());

        _pipeline_Builder = new PipelineBuilder<MyCommand>(registry, (IAmAHandlerFactoryAsync)handlerFactory);
        PipelineBuilder<MyCommand>.ClearPipelineCache();
    }

    [Fact]
    public void When_Building_An_Async_Pipeline_Allow_ForeignAttributes()
    {
        _pipeline = _pipeline_Builder.BuildAsync(new RequestContext(), false).First();

        TraceFilters().ToString().Should().Be("MyValidationHandlerAsync`1|MyObsoleteCommandHandlerAsync|MyLoggingHandlerAsync`1|");
    }

    public void Dispose()
    {
        CommandProcessor.ClearExtServiceBus();
    }

    private PipelineTracer TraceFilters()
    {
        var pipelineTracer = new PipelineTracer();
        _pipeline.DescribePath(pipelineTracer);
        return pipelineTracer;
    }
    }
}
