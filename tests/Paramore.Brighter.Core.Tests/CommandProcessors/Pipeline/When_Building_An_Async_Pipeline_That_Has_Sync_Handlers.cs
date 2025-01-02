using System;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline
{
    [Collection("CommandProcessor")]
    public class PipelineMixedHandlersAsyncTests : IDisposable
    {
        private readonly PipelineBuilder<MyCommand> _pipelineBuilder;
        private IHandleRequestsAsync<MyCommand> _pipeline;
        private Exception _exception;
        private SubscriberRegistry _subscriberRegistry;

        public PipelineMixedHandlersAsyncTests()
        {
            _subscriberRegistry = new SubscriberRegistry();
            _subscriberRegistry.RegisterAsync<MyCommand, MyMixedImplicitHandlerAsync>();

            var container = new ServiceCollection();
            container.AddTransient<MyMixedImplicitHandlerAsync>();
            container.AddTransient<MyLoggingHandler<MyCommand>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions {HandlerLifetime = ServiceLifetime.Transient});
 
            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());


            _pipelineBuilder = new PipelineBuilder<MyCommand>((IAmAHandlerFactoryAsync)handlerFactory);
            PipelineBuilder<MyCommand>.ClearPipelineCache();
        }

        [Fact]
        public void When_Building_An_Async_Pipeline_That_Has_Sync_Handlers()
        {
            var observers = _subscriberRegistry.Get<MyCommand>();
            _exception = Catch.Exception(() => _pipeline = _pipelineBuilder.BuildAsync(observers.First(), new RequestContext(), false));

            _exception.Should().NotBeNull();
            _exception.Should().BeOfType<ConfigurationException>();
            _exception.Message.Should().Contain(typeof(MyLoggingHandler<>).Name);
        }

        public void Dispose()
        {
           CommandProcessor.ClearServiceBus(); 
        }
    }
}
