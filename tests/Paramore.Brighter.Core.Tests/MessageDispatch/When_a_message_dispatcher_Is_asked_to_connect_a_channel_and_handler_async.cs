using System;
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
     
    [Collection("CommandProcessor")]
    public class MessageDispatcherRoutingAsyncTests  : IDisposable
    {
        private readonly Dispatcher _dispatcher;
        private readonly FakeChannel _channel;
        private readonly SpyCommandProcessor _commandProcessor;

        public MessageDispatcherRoutingAsyncTests()
        {
            _channel = new FakeChannel();
            _commandProcessor = new SpyCommandProcessor();

            var messageMapperRegistry = new MessageMapperRegistry(
                null,
                new SimpleMessageMapperFactoryAsync((_) => new MyEventMessageMapperAsync())
                );
            messageMapperRegistry.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();

            var connection = new Subscription<MyEvent>(
                new SubscriptionName("test"),
                noOfPerformers: 1, 
                timeoutInMilliseconds: 1000, 
                channelFactory: new InMemoryChannelFactory(_channel),
                channelName: new ChannelName("fakeChannel"), 
                routingKey: new RoutingKey("fakekey"),
                runAsync: true);
            _dispatcher = new Dispatcher(_commandProcessor, new List<Subscription> { connection }, null, messageMapperRegistry);

            var @event = new MyEvent();
            var message = new MyEventMessageMapperAsync().MapToMessage(@event).Result;
            _channel.Enqueue(message);

            _dispatcher.State.Should().Be(DispatcherState.DS_AWAITING);
            _dispatcher.Receive();
        }
#pragma warning disable xUnit1031
        
        [Fact(Timeout = 10000)]
        public void When_a_message_dispatcher_is_asked_to_connect_a_channel_and_handler_async()
        {
            Task.Delay(5000).Wait();
            _dispatcher.End().Wait();

            //should have consumed the messages in the channel
            _channel.Length.Should().Be(0);
            //should have a stopped state
            _dispatcher.State.Should().Be(DispatcherState.DS_STOPPED);
            //should have dispatched a request
            _commandProcessor.Observe<MyEvent>().Should().NotBeNull();
            //should have published async
            _commandProcessor.Commands.Should().Contain(ctype => ctype == CommandType.PublishAsync);
        }
        
        public void Dispose()
        {
            if (_dispatcher?.State == DispatcherState.DS_RUNNING)
                _dispatcher.End().Wait();
        }
    }
}
