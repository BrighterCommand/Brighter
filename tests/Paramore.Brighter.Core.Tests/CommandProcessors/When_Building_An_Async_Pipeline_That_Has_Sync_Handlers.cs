using System;
using System.Linq;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors
{
    public class PipelineMixedHandlersAsyncTests
    {
        private readonly PipelineBuilder<MyCommand> _pipelineBuilder;
        private IHandleRequestsAsync<MyCommand> _pipeline;
        private Exception _exception;

        public PipelineMixedHandlersAsyncTests()
        {
            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyMixedImplicitHandlerAsync>();

            var container = new ServiceCollection();
            container.AddTransient<MyMixedImplicitHandlerAsync>();
            container.AddTransient<MyLoggingHandler<MyCommand>>();

            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());


            _pipelineBuilder = new PipelineBuilder<MyCommand>(registry, (IAmAHandlerFactoryAsync)handlerFactory);
            PipelineBuilder<MyCommand>.ClearPipelineCache();
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
