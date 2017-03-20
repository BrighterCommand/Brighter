using FakeItEasy;
using FluentAssertions;
using Xunit;
using Paramore.Brighter.Tests.TestDoubles;

namespace Paramore.Brighter.Tests
{
    public class ContextBagVisibilityTests
    {
        private const string I_AM_A_TEST_OF_THE_CONTEXT_BAG = "I am a test of the context bag";
        private RequestContext _request_context;
        private CommandProcessor _commandProcessor;
        private MyCommand _myCommand;

        public ContextBagVisibilityTests()
        {
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyContextAwareCommandHandler>();
            var handlerFactory = new TestHandlerFactory<MyCommand, MyContextAwareCommandHandler>(() => new MyContextAwareCommandHandler());
            _request_context = new RequestContext();
            _myCommand = new MyCommand();
            MyContextAwareCommandHandler.TestString = null;

            var requestContextFactory = A.Fake<IAmARequestContextFactory>();
            A.CallTo(() => requestContextFactory.Create()).Returns(_request_context);

            _commandProcessor = new CommandProcessor(registry, handlerFactory, requestContextFactory, new PolicyRegistry());

            _request_context.Bag["TestString"] = I_AM_A_TEST_OF_THE_CONTEXT_BAG;
        }

        [Fact]
        public void When_Putting_A_Variable_Into_The_Bag_Should_Be_Accessible_In_The_Handler()
        {
            _commandProcessor.Send(_myCommand);

            //_should_have_seen_the_data_we_pushed_into_the_bag
            MyContextAwareCommandHandler.TestString.Should().Be(I_AM_A_TEST_OF_THE_CONTEXT_BAG);
            //_should_have_been_filled_by_the_handler
            _request_context.Bag["MyContextAwareCommandHandler"].Should().Be("I was called and set the context");
        }
    }
}