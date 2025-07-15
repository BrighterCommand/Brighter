using System;
using System.Collections.Generic;
using System.Linq;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline
{
    [Collection("CommandProcessor")]
    public class PipelineForCommandTests : IDisposable
    {
        private readonly PipelineBuilder<MyCommand> _chainBuilder;
        private IHandleRequests<MyCommand> _chainOfResponsibility;
        private readonly RequestContext _requestContext;

        public PipelineForCommandTests()
        {
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyCommandHandler>();
            var handlerFactory = new SimpleHandlerFactorySync(_ => new MyCommandHandler(new Dictionary<string, string>()));
            _requestContext = new RequestContext();

            _chainBuilder = new PipelineBuilder<MyCommand>(registry, handlerFactory);
        }

        [Fact]
        public void When_Creating_Context_For_A_Handler()
        {
            _chainOfResponsibility = _chainBuilder.Build(new MyCommand(), _requestContext).First();

            Assert.NotNull(_chainOfResponsibility.Context);
            Assert.Same(_requestContext, _chainOfResponsibility.Context);
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
