using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch
{
     
    [Collection("CommandProcessor")]
    public class MessageDispatcherRoutingAsyncTests  : IDisposable
    {
        private const string ChannelName = "myChannel";
        private const string Topic = "myTopic";
        private readonly Dispatcher _dispatcher;
        private readonly SpyCommandProcessor _commandProcessor;
        private readonly RoutingKey _routingKey = new(Topic);
        private readonly InternalBus _bus = new();
        private readonly FakeTimeProvider _timeProvider = new();

        public MessageDispatcherRoutingAsyncTests()
        {
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
                channelFactory: new InMemoryChannelFactory(_bus, _timeProvider),
                channelName: new ChannelName(ChannelName), 
                routingKey: _routingKey,
                runAsync: true
            );

            _dispatcher = new Dispatcher(
                _commandProcessor, 
                new List<Subscription> { connection }, 
                null, 
                messageMapperRegistry,
               requestContextFactory: new InMemoryRequestContextFactory() 
            );

            var @event = new MyEvent();
            var message = new MyEventMessageMapperAsync().MapToMessageAsync(@event, new() { Topic = connection.RoutingKey }).Result;
            
            _bus.Enqueue(message);

            _dispatcher.State.Should().Be(DispatcherState.DS_AWAITING);
            _dispatcher.Receive();
        }
#pragma warning disable xUnit1031
        
        [Fact]
        public void When_a_message_dispatcher_is_asked_to_connect_a_channel_and_handler_async()
        {
            Task.Delay(3000).Wait();
            _timeProvider.Advance(TimeSpan.FromSeconds(2)); //This will trigger requeue of not acked/rejected messages
            
            _dispatcher.End().Wait();
            
            Assert.Empty(_bus.Stream(_routingKey));
            _dispatcher.State.Should().Be(DispatcherState.DS_STOPPED);
            _commandProcessor.Observe<MyEvent>().Should().NotBeNull();
            _commandProcessor.Commands.Should().Contain(ctype => ctype == CommandType.PublishAsync);
        }
        
        public void Dispose()
        {
            if (_dispatcher?.State == DispatcherState.DS_RUNNING)
                _dispatcher.End().Wait();
        }
    }
}
