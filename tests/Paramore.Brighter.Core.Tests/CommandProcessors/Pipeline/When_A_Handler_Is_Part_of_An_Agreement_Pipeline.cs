using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline
{
    public class PipelineBuilderAgreementAsyncTests
    {
        private readonly PipelineBuilder<MyCommand> _pipelineBuilder;
        private IHandleRequests<MyCommand>? _pipeline;
        public PipelineBuilderAgreementAsyncTests()
        {
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand>(((request, context) =>
            {
                var myCommand = request as MyCommand;
                if (myCommand?.Value == "first")
                    return[typeof(MyImplicitHandler)];
                return[typeof(MyCommandHandler)];
            }), [typeof(MyImplicitHandler), typeof(MyCommandHandler)]);
            var container = new ServiceCollection();
            container.AddTransient<MyImplicitHandler>();
            container.AddTransient<MyLoggingHandler<MyCommand>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });
            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());
            _pipelineBuilder = new PipelineBuilder<MyCommand>(registry, (IAmAHandlerFactorySync)handlerFactory);
        }

        [Test]
        public async Task When_A_Handler_Is_Part_of_A_Pipeline()
        {
            _pipeline = _pipelineBuilder.Build(new MyCommand { Value = "first" }, new RequestContext()).First();
            var trace = TracePipeline().ToString();
            await Assert.That(trace).Contains("MyImplicitHandler");
            await Assert.That(trace).Contains("MyLoggingHandler");
        }

        private PipelineTracer TracePipeline()
        {
            var pipelineTracer = new PipelineTracer();
            _pipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }
    }
}