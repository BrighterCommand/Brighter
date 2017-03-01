using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using paramore.brighter.commandprocessor.tests.nunit.CommandProcessors.TestDoubles;
using paramore.brighter.commandprocessor.tests.nunit.MessageDispatch.TestDoubles;
using paramore.brighter.serviceactivator;
using paramore.brighter.serviceactivator.TestHelpers;

namespace paramore.brighter.commandprocessor.tests.nunit.MessageDispatch
{
    [TestFixture]
    public class MessageDispatcherRoutingAsyncTests
    {
        private Dispatcher _dispatcher;
        private FakeChannel _channel;
        private SpyCommandProcessor _commandProcessor;

        [SetUp]
        public void Establish()
        {
            _channel = new FakeChannel();
            _commandProcessor = new SpyCommandProcessor();

            var messageMapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(() => new MyEventMessageMapper()));
            messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

            var connection = new Connection(
                name: new ConnectionName("test"), 
                dataType: typeof(MyEvent), 
                noOfPerformers: 1, 
                timeoutInMilliseconds: 1000, 
                channelFactory: new InMemoryChannelFactory(_channel),
                channelName: new ChannelName("fakeChannel"), 
                routingKey: "fakekey",
                isAsync: true);
            _dispatcher = new Dispatcher(_commandProcessor, messageMapperRegistry, new List<Connection> { connection });

            var @event = new MyEvent();
            var message = new MyEventMessageMapper().MapToMessage(@event);
            _channel.Add(message);

            _dispatcher.State.ShouldEqual(DispatcherState.DS_AWAITING);
            _dispatcher.Receive();
        }

        [Test]
        public void When_A_Message_Dispatcher_Is_Asked_To_Connect_A_Channel_And_Handler_Async()
        {
            Task.Delay(1000).Wait();
            _dispatcher.End().Wait();


            //_should_have_consumed_the_messages_in_the_channel
            _channel.Length.ShouldEqual(0);
            //_should_have_a_stopped_state
            _dispatcher.State.ShouldEqual(DispatcherState.DS_STOPPED);
            //_should_have_dispatched_a_request
            _commandProcessor.Observe<MyEvent>().ShouldNotBeNull();
            //_should_have_published_async
            _commandProcessor.Commands.Any(ctype => ctype == CommandType.PublishAsync).ShouldBeTrue();
        }

    }
}
