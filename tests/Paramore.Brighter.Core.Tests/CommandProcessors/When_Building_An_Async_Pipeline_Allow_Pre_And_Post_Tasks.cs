using System.Linq;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.Core.Tests.CommandProcessors
{
    public class PipelinePreAndPostFiltersAsyncTests
    {
        private readonly PipelineBuilder<MyCommand> _pipelineBuilder;
        private IHandleRequestsAsync<MyCommand> _pipeline;

        public PipelinePreAndPostFiltersAsyncTests()
        {
            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyPreAndPostDecoratedHandlerAsync>();

            var container = new ServiceCollection();
            container.AddTransient<MyPreAndPostDecoratedHandlerAsync>();
            container.AddTransient<MyValidationHandlerAsync<MyCommand>>();
            container.AddTransient<MyLoggingHandlerAsync<MyCommand>>();

            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());
            
            _pipelineBuilder = new PipelineBuilder<MyCommand>(registry,(IAmAHandlerFactoryAsync)handlerFactory);
            PipelineBuilder<MyCommand>.ClearPipelineCache();
        }

        private void When_Building_An_Async_Pipeline_Allow_Pre_And_Post_Tasks()
        {
            _pipeline = _pipelineBuilder.BuildAsync(new RequestContext(), false).First();

            TraceFilters().ToString().Should().Be("MyValidationHandlerAsync`1|MyPreAndPostDecoratedHandlerAsync|MyLoggingHandlerAsync`1|");
        }

        private PipelineTracer TraceFilters()
        {
            var pipelineTracer = new PipelineTracer();
            _pipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }
    }
}
