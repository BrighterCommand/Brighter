using System;
using System.Collections.Generic;
using System.Linq;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline
{
    public class PipelineForCommandAsyncTests
    {
        private PipelineBuilder<MyCommand> _chainBuilder;
        private IHandleRequestsAsync<MyCommand> _chainOfResponsibility;
        private RequestContext _requestContext;
        private readonly IDictionary<string, string> _receivedMessages = new Dictionary<string, string>();
        public PipelineForCommandAsyncTests()
        {
            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyCommandHandlerAsync>();
            var handlerFactory = new SimpleHandlerFactoryAsync(_ => new MyCommandHandlerAsync(_receivedMessages));
            _requestContext = new RequestContext();
            _chainBuilder = new PipelineBuilder<MyCommand>(registry, asyncHandlerFactory: handlerFactory);
        }

        [Test]
        public async Task When_Creating_Context_For_A_Handler_Async()
        {
            _chainOfResponsibility = _chainBuilder.BuildAsync(new MyCommand(), _requestContext, false).First();
            await Assert.That(_chainOfResponsibility.Context).IsNotNull();
            await Assert.That(_chainOfResponsibility.Context).IsSameReferenceAs(_requestContext);
        }
    }
}