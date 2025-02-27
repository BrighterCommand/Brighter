using System;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Context
{
    [Collection("CommandProcessor")]
    public class ContextBagVisibilityTests : IDisposable
    {
        private const string I_AM_A_TEST_OF_THE_CONTEXT_BAG = "I am a test of the context bag";
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand;

        public ContextBagVisibilityTests()
        {
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyContextAwareCommandHandler>();
            var handlerFactory = new SimpleHandlerFactorySync(_ => new MyContextAwareCommandHandler());
            _myCommand = new MyCommand();
            MyContextAwareCommandHandler.TestString = null;

            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), new InMemorySchedulerFactory());
            PipelineBuilder<MyCommand>.ClearPipelineCache();
        }

        [Fact]
        public void When_Putting_A_Variable_Into_The_Bag_Should_Be_Accessible_In_The_Handler()
        {
            var requestContext = new RequestContext();
            requestContext.Bag["TestString"] = I_AM_A_TEST_OF_THE_CONTEXT_BAG;
            
            _commandProcessor.Send(_myCommand, requestContext);

            MyContextAwareCommandHandler.TestString.Should().Be(I_AM_A_TEST_OF_THE_CONTEXT_BAG);
            requestContext.Bag["MyContextAwareCommandHandler"].Should().Be("I was called and set the context");
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
