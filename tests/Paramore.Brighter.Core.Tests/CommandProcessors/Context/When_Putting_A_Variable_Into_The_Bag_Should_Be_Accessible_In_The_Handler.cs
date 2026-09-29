using System;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Polly.Registry;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Context
{
    public class ContextBagVisibilityTests
    {
        private const string I_AM_A_TEST_OF_THE_CONTEXT_BAG = "I am a test of the context bag";
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand;
        private readonly MyContextAwareCommandHandler _handler;
        public ContextBagVisibilityTests()
        {
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyContextAwareCommandHandler>();
            _handler = new MyContextAwareCommandHandler();
            var handlerFactory = new SimpleHandlerFactorySync(_ => _handler);
            _myCommand = new MyCommand();
            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
        }

        [Test]
        public async Task When_Putting_A_Variable_Into_The_Bag_Should_Be_Accessible_In_The_Handler()
        {
            var requestContext = new RequestContext();
            requestContext.Bag["TestString"] = I_AM_A_TEST_OF_THE_CONTEXT_BAG;
            _commandProcessor.Send(_myCommand, requestContext);
            await Assert.That(_handler.TestString).IsEqualTo(I_AM_A_TEST_OF_THE_CONTEXT_BAG);
            await Assert.That(requestContext.Bag["MyContextAwareCommandHandler"]).IsEqualTo("I was called and set the context");
        }
    }
}
