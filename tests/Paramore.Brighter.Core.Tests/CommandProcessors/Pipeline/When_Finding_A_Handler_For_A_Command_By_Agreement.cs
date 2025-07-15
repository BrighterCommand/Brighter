using System;
using System.Collections.Generic;
using System.Linq;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline
{
    [Collection("CommandProcessor")]
    public class PipelineBuildForAgreementTests : IDisposable
    {
        private readonly PipelineBuilder<MyCommand> _pipelineBuilder;
        private IHandleRequests<MyCommand>? _pipeline;

        public PipelineBuildForAgreementTests ()
        {
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand>(router: (request, context) =>
            {
                var command = request as MyCommand;
                if (command.Value == "new")
                    return [typeof(MyCommandHandler)];

                return [typeof(MyObsoleteCommandHandler)];
            },
                    [typeof(MyCommandHandler), typeof(MyObsoleteCommandHandler)]
            );
            var handlerFactory = new SimpleHandlerFactorySync(factoryMethod: _ => new MyCommandHandler(new Dictionary<string, string>()));

            _pipelineBuilder = new PipelineBuilder<MyCommand>(subscriberRegistry: registry, syncHandlerFactory: handlerFactory);
            PipelineBuilder<MyCommand>.ClearPipelineCache();
        }

        [Fact]
        public void When_Finding_A_Handler_For_A_Command()
        {
            _pipeline = _pipelineBuilder.Build(new MyCommand {Value = "new"}, new RequestContext()).First();

            Assert.IsType<MyCommandHandler>(_pipeline);
            Assert.Equal("MyCommandHandler|", TracePipeline().ToString());
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
