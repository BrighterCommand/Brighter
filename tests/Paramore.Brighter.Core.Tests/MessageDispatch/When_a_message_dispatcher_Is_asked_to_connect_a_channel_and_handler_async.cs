using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.TestHelpers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch
{
    public class MessageDispatcherRoutingAsyncTests : IAsyncLifetime
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

            var connection = new Subscription<MyEvent>(
                new SubscriptionName("test"),
                noOfPerformers: 1, 
                timeoutInMilliseconds: 1000, 
                channelFactory: new InMemoryChannelFactory(_channel),
                channelName: new ChannelName("fakeChannel"), 
                routingKey: new RoutingKey("fakekey"),
                runAsync: true);
            _dispatcher = new Dispatcher(_commandProcessor, messageMapperRegistry, new List<Subscription> { connection });

            var @event = new MyEvent();
            var message = new MyEventMessageMapper().MapToMessage(@event);
            _channel.Enqueue(message);

            _dispatcher.State.Should().Be(DispatcherState.DS_AWAITING);
        }
        
        [Fact]
        public async Task When_a_message_dispatcher_is_asked_to_connect_a_channel_and_handler_async()
        {
            await _dispatcher.End();

            //should have consumed the messages in the channel
            _channel.Length.Should().Be(0);
            //should have a stopped state
            _dispatcher.State.Should().Be(DispatcherState.DS_STOPPED);
            //should have dispatched a request
            _commandProcessor.Observe<MyEvent>().Should().NotBeNull();
            //should have published async
            _commandProcessor.Commands.Should().Contain(commandType => commandType == CommandType.PublishAsync);
        }

        public Task InitializeAsync()
        {
            _dispatcher.Receive();
            var completionSource = new TaskCompletionSource<IDispatcher>();
            completionSource.SetResult(_dispatcher);

            return completionSource.Task;
        }

        public Task DisposeAsync()
        {
            var completionSource = new TaskCompletionSource<object>();
            completionSource.SetResult(null);
            return completionSource.Task;
        }
    }
}
