using System;
using System.Linq;
using FluentAssertions;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using TinyIoC;
using Xunit;

namespace Paramore.Brighter.Tests.CommandProcessors
{
    public class PipelineMixedHandlersAsyncTests
    {
        private PipelineBuilder<MyCommand> _pipelineBuilder;
        private IHandleRequestsAsync<MyCommand> _pipeline;
        private Exception _exception;

        public PipelineMixedHandlersAsyncTests()
        {
            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyMixedImplicitHandlerAsync>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactoryAsync(container);
            container.Register<IHandleRequestsAsync<MyCommand>, MyMixedImplicitHandlerAsync>();
            container.Register<IHandleRequests<MyCommand>, MyLoggingHandler<MyCommand>>();

            _pipelineBuilder = new PipelineBuilder<MyCommand>(registry, handlerFactory);
        }

        [Fact]
        public void When_Building_An_Async_Pipeline_That_Has_Sync_Handlers()
        {
            _exception = Catch.Exception(() => _pipeline = _pipelineBuilder.BuildAsync(new RequestContext(), false).First());

            _exception.Should().NotBeNull();
            _exception.Should().BeOfType<ConfigurationException>();
            _exception.Message.Should().Contain(typeof(MyLoggingHandler<>).Name);
        }
    }
}
