using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.TestHelpers;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Tests.MessageDispatch.TestDoubles;

namespace Paramore.Brighter.Tests.MessageDispatch
{
    public class MessageDispatcherRoutingAsyncTests
    {
        private readonly Dispatcher _dispatcher;
        private readonly FakeChannel _channel;
        private readonly SpyCommandProcessor _commandProcessor;

        public MessageDispatcherRoutingAsyncTests()
        {
            _channel = new FakeChannel();
            _commandProcessor = new SpyCommandProcessor();

            var messageMapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory((_) => new MyEventMessageMapper()));
            messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

            var connection = new Connection<MyEvent>(
                new ConnectionName("test"),
                noOfPerformers: 1, 
                timeoutInMilliseconds: 1000, 
                channelFactory: new InMemoryChannelFactory(_channel),
                channelName: new ChannelName("fakeChannel"), 
                routingKey: new RoutingKey("fakekey"),
                isAsync: true);
            _dispatcher = new Dispatcher(_commandProcessor, messageMapperRegistry, new List<Connection> { connection });

            var @event = new MyEvent();
            var message = new MyEventMessageMapper().MapToMessage(@event);
            _channel.Add(message);

            _dispatcher.State.Should().Be(DispatcherState.DS_AWAITING);
            _dispatcher.Receive();
        }

        [Fact]
        public void When_a_message_dispatcher_is_asked_to_connect_a_channel_and_handler_async()
        {
            Task.Delay(1000).Wait();
            _dispatcher.End().Wait();

            //_should_have_consumed_the_messages_in_the_channel
            _channel.Length.Should().Be(0);
            //_should_have_a_stopped_state
            _dispatcher.State.Should().Be(DispatcherState.DS_STOPPED);
            //_should_have_dispatched_a_request
            _commandProcessor.Observe<MyEvent>().Should().NotBeNull();
            //_should_have_published_async
            _commandProcessor.Commands.Should().Contain(ctype => ctype == CommandType.PublishAsync);
        }
    }
}
