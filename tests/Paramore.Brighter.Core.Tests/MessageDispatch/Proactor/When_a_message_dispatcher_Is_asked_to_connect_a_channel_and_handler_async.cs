using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Proactor
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

            var subscription = new Subscription<MyEvent>(
                new SubscriptionName("test"),
                noOfPerformers: 1, 
                timeOut: TimeSpan.FromMilliseconds(1000), 
                channelFactory: new InMemoryChannelFactory(_bus, _timeProvider),
                channelName: new ChannelName(ChannelName), 
                routingKey: _routingKey,
                messagePumpType: MessagePumpType.Proactor
            );

            _dispatcher = new Dispatcher(
                _commandProcessor, 
                new List<Subscription> { subscription }, 
                null, 
                messageMapperRegistry,
               requestContextFactory: new InMemoryRequestContextFactory() 
            );

            var @event = new MyEvent {Data = 4};
            var message = new MyEventMessageMapperAsync().MapToMessageAsync(@event, new() { Topic = _routingKey }).Result;
            
            _bus.Enqueue(message);

            Assert.Equal(DispatcherState.DS_AWAITING, _dispatcher.State);
            _dispatcher.Receive();
            
        }
#pragma warning disable xUnit1031
        
        [Fact]
        public async Task When_a_message_dispatcher_is_asked_to_connect_a_channel_and_handler_async()
        {
            await Task.Delay(5000);
            
            _timeProvider.Advance(TimeSpan.FromSeconds(2)); //This will trigger requeue of not acked/rejected messages
            
            await _dispatcher.End();
            
            Assert.Equal(DispatcherState.DS_STOPPED, _dispatcher.State);
            Assert.NotNull(_commandProcessor.Observe<MyEvent>());
            Assert.Contains(CommandType.PublishAsync, _commandProcessor.Commands);
            Assert.Empty(_bus.Stream(_routingKey));
        }
        
        public void Dispose()
        {
            if (_dispatcher?.State == DispatcherState.DS_RUNNING)
                _dispatcher.End().Wait();
        }
    }
}
