using System;
using System.Linq;
using FluentAssertions;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Tests.CommandProcessors
{
    public class PipelineCleanupTests
    {
        private readonly PipelineBuilder<MyCommand> _pipelineBuilder;
        private static string s_released;

        public PipelineCleanupTests()
        {
            s_released = string.Empty;

            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyPreAndPostDecoratedHandler>();
            registry.Register<MyCommand, MyLoggingHandler<MyCommand>>();

            var handlerFactory = new CheapHandlerFactory();

            _pipelineBuilder = new PipelineBuilder<MyCommand>(registry, handlerFactory);
            _pipelineBuilder.Build(new RequestContext()).Any();
        }

        internal class CheapHandlerFactory : IAmAHandlerFactory
        {
            public IHandleRequests Create(Type handlerType)
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

            public void Release(IHandleRequests handler)
            {
                var disposable = handler as IDisposable;
                disposable?.Dispose();

                s_released += "|" + handler.Name;
            }
        }


        [Fact]
        public void When_We_Have_Exercised_The_Pipeline_Cleanup_Its_Handlers()
        {
            _pipelineBuilder.Dispose();

            //_should_have_called_dispose_on_instances_from_ioc
            MyPreAndPostDecoratedHandler.DisposeWasCalled.Should().BeTrue();
            //_should_have_called_dispose_on_instances_from_pipeline_builder
            MyLoggingHandler<MyCommand>.DisposeWasCalled.Should().BeTrue();
            //_should_have_called_release_on_all_handlers
            s_released.Should().Be("|MyValidationHandler`1|MyPreAndPostDecoratedHandler|MyLoggingHandler`1|MyLoggingHandler`1");
        }
    }
}