using System;
using System.Collections.Generic;
using System.Linq;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline
{
    [Collection("CommandProcessor")]
    public class PipelineForCommandAsyncTests : IDisposable
    {
        private static PipelineBuilder<MyCommand> _chainBuilder;
        private static IHandleRequestsAsync<MyCommand> _chainOfResponsibility;
        private static RequestContext _requestContext;
        private readonly IDictionary<string, string> _receivedMessages = new Dictionary<string, string>();

        public PipelineForCommandAsyncTests()
        {
            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyCommandHandlerAsync>();
            var handlerFactory = new SimpleHandlerFactoryAsync(_ => new MyCommandHandlerAsync(_receivedMessages));
            _requestContext = new RequestContext();

            _chainBuilder = new PipelineBuilder<MyCommand>(registry, asyncHandlerFactory: handlerFactory);
        }

        [Fact]
        public void When_Building_A_Handler_For_An_Async_Command()
        {
            _chainOfResponsibility = _chainBuilder.BuildAsync(_requestContext, false).First();

            Assert.NotNull(_chainOfResponsibility.Context);
            Assert.Same(_requestContext, _chainOfResponsibility.Context);
        }

        public void Dispose()
        {
           CommandProcessor.ClearServiceBus();
        }
    }
}
