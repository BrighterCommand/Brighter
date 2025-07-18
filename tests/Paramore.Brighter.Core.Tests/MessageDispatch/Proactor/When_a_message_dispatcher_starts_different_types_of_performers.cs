using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Proactor
{
    [Collection("CommandProcessor")]
    public class MessageDispatcherMultipleConnectionTestsAsync : IDisposable
    {
        private readonly Dispatcher _dispatcher;
        private int _numberOfConsumers;
        private readonly InternalBus _bus = new();
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly RoutingKey _commandRoutingKey = new("myCommand");
        private readonly RoutingKey _eventRoutingKey = new("myEvent");

        public MessageDispatcherMultipleConnectionTestsAsync()
        {
            var commandProcessor = new SpyCommandProcessor();

            var container = new ServiceCollection();
            container.AddTransient<MyEventMessageMapperAsync>();
            container.AddTransient<MyCommandMessageMapperAsync>();

            var messageMapperRegistry = new MessageMapperRegistry(
                null,
                new ServiceProviderMapperFactoryAsync(container.BuildServiceProvider())
                );
            messageMapperRegistry.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();
            messageMapperRegistry.RegisterAsync<MyCommand, MyCommandMessageMapperAsync>();

            var myEventConnection = new Subscription<MyEvent>(
                new SubscriptionName("test"), 
                noOfPerformers: 1, 
                timeOut: TimeSpan.FromMilliseconds(1000), 
                channelFactory: new InMemoryChannelFactory(_bus, _timeProvider), 
                messagePumpType: MessagePumpType.Proactor,
                channelName: new ChannelName("fakeEventChannel"), 
                routingKey: _eventRoutingKey
            );
            var myCommandConnection = new Subscription<MyCommand>(
                new SubscriptionName("anothertest"), 
                noOfPerformers: 1, 
                timeOut: TimeSpan.FromMilliseconds(1000), 
                channelFactory: new InMemoryChannelFactory(_bus, _timeProvider), 
                channelName: new ChannelName("fakeCommandChannel"), 
                messagePumpType: MessagePumpType.Proactor, 
                routingKey: _commandRoutingKey
                );
            _dispatcher = new Dispatcher(commandProcessor, new List<Subscription> { myEventConnection, myCommandConnection }, messageMapperRegistryAsync: messageMapperRegistry);

            var @event = new MyEvent();
            var eventMessage = new MyEventMessageMapperAsync().MapToMessageAsync(@event, new Publication{Topic = _eventRoutingKey})
                .GetAwaiter()
                .GetResult();
            
            _bus.Enqueue(eventMessage);

            var command = new MyCommand();
            var commandMessage = new MyCommandMessageMapperAsync().MapToMessageAsync(command, new Publication{Topic = _commandRoutingKey})
                .GetAwaiter()
                .GetResult();
            
            _bus.Enqueue(commandMessage);
            
            Assert.Equal(DispatcherState.DS_AWAITING, _dispatcher.State);
            _dispatcher.Receive();
        }


        [Fact]
        public async Task When_A_Message_Dispatcher_Starts_Different_Types_Of_Performers()
        {
            await Task.Delay(1000);
            
            _numberOfConsumers = _dispatcher.Consumers.Count();
            
            _timeProvider.Advance(TimeSpan.FromSeconds(2)); //This will trigger requeue of not acked/rejected messages
            
            await _dispatcher.End();

            Assert.Empty(_bus.Stream(_eventRoutingKey));
            Assert.Empty(_bus.Stream(_commandRoutingKey));
            Assert.Equal(DispatcherState.DS_STOPPED, _dispatcher.State);
            Assert.Empty(_dispatcher.Consumers);
            Assert.Equal(2, _numberOfConsumers);
        }
        
        public void Dispose()
        {
            if (_dispatcher?.State == DispatcherState.DS_RUNNING)
                _dispatcher.End().Wait();
        }

    }

}
