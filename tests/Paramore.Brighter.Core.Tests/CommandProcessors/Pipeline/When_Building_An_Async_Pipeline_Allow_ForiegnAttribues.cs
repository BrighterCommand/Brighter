using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline
{
    public class PipelineForiegnAttributesAsyncTests
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
            container.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });
            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());
            _pipeline_Builder = new PipelineBuilder<MyCommand>(registry, (IAmAHandlerFactoryAsync)handlerFactory);
        }

        [Test]
        public async Task When_Building_An_Async_Pipeline_Allow_ForeignAttributes()
        {
            _pipeline = _pipeline_Builder.BuildAsync(new MyCommand(), new RequestContext(), false).First();
            var trace = TraceFilters().ToString();
            await Assert.That(trace).IsEqualTo("MyValidationHandlerAsync`1|MyObsoleteCommandHandlerAsync|MyLoggingHandlerAsync`1|");
        }

        private PipelineTracer TraceFilters()
        {
            var pipelineTracer = new PipelineTracer();
            _pipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }
    }
}