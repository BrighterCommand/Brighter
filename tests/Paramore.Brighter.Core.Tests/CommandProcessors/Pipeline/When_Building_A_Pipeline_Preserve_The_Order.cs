using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline
{
    [Collection("CommandProcessor")]
    public class PipelineOrderingTests : IDisposable
    {
        private readonly PipelineBuilder<MyCommand> _pipelineBuilder;
        private IHandleRequests<MyCommand> _pipeline;

        public PipelineOrderingTests()
        {
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyDoubleDecoratedHandler>();

            var container = new ServiceCollection();
            container.AddTransient<MyDoubleDecoratedHandler>();
            container.AddTransient<MyLoggingHandler<MyCommand>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions {HandlerLifetime = ServiceLifetime.Transient});

            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());

            _pipelineBuilder = new PipelineBuilder<MyCommand>(registry, (IAmAHandlerFactorySync)handlerFactory);
        }

        [Fact]
        public void When_Building_A_Pipeline_Preserve_The_Order()
        {
            _pipeline = _pipelineBuilder.Build(new RequestContext()).First();

            Assert.Equal("MyLoggingHandler`1|MyValidationHandler`1|MyDoubleDecoratedHandler|", PipelineTracer().ToString());
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
