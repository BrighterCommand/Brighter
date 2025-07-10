using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline
{
    [Collection("CommandProcessor")]
    public class PipelineOrderingAsyncTests: IDisposable
    {
        private readonly PipelineBuilder<MyCommand> _pipeline_Builder;
        private IHandleRequestsAsync<MyCommand> _pipeline;

        public PipelineOrderingAsyncTests()
        {
            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyDoubleDecoratedHandlerAsync>();

            var container = new ServiceCollection();
            container.AddTransient<MyDoubleDecoratedHandlerAsync>();
            container.AddTransient<MyValidationHandlerAsync<MyCommand>>();
            container.AddTransient<MyLoggingHandlerAsync<MyCommand>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions {HandlerLifetime = ServiceLifetime.Transient});

            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());

            _pipeline_Builder = new PipelineBuilder<MyCommand>(registry, (IAmAHandlerFactoryAsync)handlerFactory);
            PipelineBuilder<MyCommand>.ClearPipelineCache();
        }

        [Fact]
        public void When_Building_An_Async_Pipeline_Preserve_The_Order()
        {
            _pipeline = _pipeline_Builder.BuildAsync(new MyCommand(), new RequestContext(), false).First();

            var trace = PipelineTracer().ToString();
            Assert.Equal("MyLoggingHandlerAsync`1|MyValidationHandlerAsync`1|MyDoubleDecoratedHandlerAsync|", trace);
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }

        private PipelineTracer PipelineTracer()
        {
            var pipelineTracer = new PipelineTracer();
            _pipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }
    }
}
