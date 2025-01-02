using System;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline
{
    [Collection("CommandProcessor")]
    public class PipelinePreAndPostFiltersAsyncTests : IDisposable
    {
        private readonly PipelineBuilder<MyCommand> _pipelineBuilder;
        private IHandleRequestsAsync<MyCommand> _pipeline;
        private SubscriberRegistry _subscriberRegistry;

        public PipelinePreAndPostFiltersAsyncTests()
        {
            _subscriberRegistry = new SubscriberRegistry();
            _subscriberRegistry.RegisterAsync<MyCommand, MyPreAndPostDecoratedHandlerAsync>();

            var container = new ServiceCollection();
            container.AddTransient<MyPreAndPostDecoratedHandlerAsync>();
            container.AddTransient<MyValidationHandlerAsync<MyCommand>>();
            container.AddTransient<MyLoggingHandlerAsync<MyCommand>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions {HandlerLifetime = ServiceLifetime.Transient});
 
            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());
            
            _pipelineBuilder = new PipelineBuilder<MyCommand>((IAmAHandlerFactoryAsync)handlerFactory);
            PipelineBuilder<MyCommand>.ClearPipelineCache();
        }

        [Fact]
        private void When_Building_An_Async_Pipeline_Allow_Pre_And_Post_Tasks()
        {
            var observers = _subscriberRegistry.Get<MyCommand>();
            _pipeline = _pipelineBuilder.BuildAsync(observers.First(), new RequestContext(), false);

            TraceFilters().ToString().Should().Be("MyValidationHandlerAsync`1|MyPreAndPostDecoratedHandlerAsync|MyLoggingHandlerAsync`1|");
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
