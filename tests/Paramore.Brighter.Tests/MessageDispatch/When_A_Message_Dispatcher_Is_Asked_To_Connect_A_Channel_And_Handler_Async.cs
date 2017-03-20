using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.TestHelpers;
using Paramore.Brighter.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.Tests.TestDoubles;

namespace Paramore.Brighter.Tests.MessageDispatch
{
    public class MessageDispatcherRoutingAsyncTests
    {
        private Dispatcher _dispatcher;
        private FakeChannel _channel;
        private SpyCommandProcessor _commandProcessor;

        public MessageDispatcherRoutingAsyncTests()
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

            Assert.AreEqual(DispatcherState.DS_AWAITING, _dispatcher.State);
            _dispatcher.Receive();
        }

        [Fact]
        public void When_A_Message_Dispatcher_Is_Asked_To_Connect_A_Channel_And_Handler_Async()
        {
            Task.Delay(1000).Wait();
            _dispatcher.End().Wait();


            //_should_have_consumed_the_messages_in_the_channel
            Assert.AreEqual(0, _channel.Length);
            //_should_have_a_stopped_state
            Assert.AreEqual(DispatcherState.DS_STOPPED, _dispatcher.State);
            //_should_have_dispatched_a_request
            Assert.NotNull(_commandProcessor.Observe<MyEvent>());
            //_should_have_published_async
            Assert.True(_commandProcessor.Commands.Any(ctype => ctype == CommandType.PublishAsync));
        }

    }
}
