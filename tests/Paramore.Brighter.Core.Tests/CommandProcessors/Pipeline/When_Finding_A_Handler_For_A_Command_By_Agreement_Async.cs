using System;
using System.Collections.Generic;
using System.Linq;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline
{
    [Collection("CommandProcessor")]
    public class PipelineBuildForAgreementAsyncTests : IDisposable
    {
        private readonly PipelineBuilder<MyCommand> _pipelineBuilder;
        private IHandleRequestsAsync<MyCommand>? _pipeline;

        public PipelineBuildForAgreementAsyncTests ()
        {
            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand>(router: (request, context) =>
            {
                var command = request as MyCommand;
                if (command.Value == "new")
                    return [typeof(MyCommandHandlerAsync)];

                return [typeof(MyObsoleteCommandHandlerAsync)];
            },
                    [typeof(MyCommandHandlerAsync), typeof(MyObsoleteCommandHandlerAsync)]
            );
            var handlerFactory = new SimpleHandlerFactoryAsync(factoryMethod: _ => new MyCommandHandlerAsync(new Dictionary<string, string>()));

            _pipelineBuilder = new PipelineBuilder<MyCommand>(subscriberRegistry: registry, asyncHandlerFactory: handlerFactory);
            PipelineBuilder<MyCommand>.ClearPipelineCache();
        }

        [Fact]
        public void When_Finding_A_Handler_For_A_Command()
        {
            _pipeline = _pipelineBuilder.BuildAsync(new MyCommand {Value = "new"}, new RequestContext(), true).First();

            Assert.IsType<MyCommandHandlerAsync>(_pipeline);
            Assert.Equal("MyCommandHandlerAsync|", TracePipeline().ToString());
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
