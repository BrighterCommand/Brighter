using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline
{
    public class PipelineBuilderAgreementTests
    {
        private readonly PipelineBuilder<MyCommand> _pipelineBuilder;
        private IHandleRequestsAsync<MyCommand>? _pipeline;
        public PipelineBuilderAgreementTests()
        {
            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand>(((request, context) =>
            {
                var myCommand = request as MyCommand;
                if (myCommand?.Value == "first")
                    return[typeof(MyImplicitHandlerAsync)];
                return[typeof(MyCommandHandlerAsync)];
            }), [typeof(MyImplicitHandlerAsync), typeof(MyCommandHandlerAsync)]);
            var container = new ServiceCollection();
            container.AddTransient<MyImplicitHandlerAsync>();
            container.AddTransient<MyLoggingHandlerAsync<MyCommand>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });
            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());
            _pipelineBuilder = new PipelineBuilder<MyCommand>(registry, (IAmAHandlerFactoryAsync)handlerFactory);
        }

        [Test]
        public async Task When_A_Handler_Is_Part_of_A_Pipeline()
        {
            _pipeline = _pipelineBuilder.BuildAsync(new MyCommand { Value = "first" }, new RequestContext(), true).First();
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