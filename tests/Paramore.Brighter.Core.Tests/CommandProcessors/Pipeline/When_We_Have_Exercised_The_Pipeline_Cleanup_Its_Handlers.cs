using System;
using System.Linq;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Pipeline
{
    [Collection("CommandProcessor")]
    public class PipelineCleanupTests : IDisposable
    {
        private readonly PipelineBuilder<MyCommand> _pipelineBuilder;
        private static string s_released;

        public PipelineCleanupTests()
        {
            s_released = string.Empty;

            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyPreAndPostDecoratedHandler>();
            registry.Register<MyCommand, MyLoggingHandler<MyCommand>>();

            var handlerFactory = new CheapHandlerFactorySync();

            _pipelineBuilder = new PipelineBuilder<MyCommand>(registry, handlerFactory);
            _pipelineBuilder.Build(new RequestContext()).Any();
        }

        [Fact]
        public void When_We_Have_Exercised_The_Pipeline_Cleanup_Its_Handlers()
        {
            _pipelineBuilder.Dispose();

            //_should_have_called_dispose_on_instances_from_ioc
            Assert.True(MyPreAndPostDecoratedHandler.DisposeWasCalled);
            //_should_have_called_dispose_on_instances_from_pipeline_builder
            Assert.True(MyLoggingHandler<MyCommand>.DisposeWasCalled);
            //_should_have_called_release_on_all_handlers
            Assert.Equal("|MyValidationHandler`1|MyPreAndPostDecoratedHandler|MyLoggingHandler`1|MyLoggingHandler`1", s_released);
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
        
        internal sealed class CheapHandlerFactorySync : IAmAHandlerFactorySync
        {
            public IHandleRequests Create(Type handlerType, IAmALifetime lifetime)
            {
                if (handlerType == typeof(MyPreAndPostDecoratedHandler))
                {
                    return new MyPreAndPostDecoratedHandler();
                }
                if (handlerType == typeof(MyLoggingHandler<MyCommand>))
                {
                    return new MyLoggingHandler<MyCommand>();
                }
                if (handlerType == typeof(MyValidationHandler<MyCommand>))
                {
                    return new MyValidationHandler<MyCommand>();
                }
                return null;
            }

            public void Release(IHandleRequests handler, IAmALifetime lifetime)
            {
                var disposable = handler as IDisposable;
                disposable?.Dispose();

                s_released += "|" + handler.Name;
            }
        }
    }
}
