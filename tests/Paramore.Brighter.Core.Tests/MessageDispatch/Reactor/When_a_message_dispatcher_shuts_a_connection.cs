using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Reactor
{
    [Collection("CommandProcessor")]
    public class MessageDispatcherShutConnectionTests : IDisposable
    {
        private const string Topic = "fakekey";
        private const string ChannelName = "fakeChannel";
        private readonly Dispatcher _dispatcher;
        private readonly Subscription _subscription;
        private readonly RoutingKey _routingKey = new(Topic);
        private readonly FakeTimeProvider _timeProvider = new();

        public MessageDispatcherShutConnectionTests()
        {
            InternalBus bus = new();
            
            IAmACommandProcessor commandProcessor = new SpyCommandProcessor();

            var messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory((_) => new MyEventMessageMapper()),
                null);
            messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

            _subscription = new Subscription<MyEvent>(
                new SubscriptionName("test"), 
                noOfPerformers: 3, 
                timeOut: TimeSpan.FromMilliseconds(1000), 
                channelFactory: new InMemoryChannelFactory(bus, _timeProvider), 
                channelName: new ChannelName(ChannelName), 
                messagePumpType: MessagePumpType.Reactor,
                routingKey: _routingKey
            );
            _dispatcher = new Dispatcher(commandProcessor, new List<Subscription> { _subscription }, messageMapperRegistry);

            var @event = new MyEvent();
            var message = new MyEventMessageMapper().MapToMessage(@event, new Publication{ Topic = _subscription.RoutingKey});
            for (var i = 0; i < 6; i++)
                bus.Enqueue(message);

            Assert.Equal(DispatcherState.DS_AWAITING, _dispatcher.State);
            _dispatcher.Receive();
            
        }

        [Fact]
        public async Task When_A_Message_Dispatcher_Shuts_A_Connection()
        {
            await Task.Delay(1000);
            _dispatcher.Shut(_subscription);
            await _dispatcher.End();

            Assert.DoesNotContain(_dispatcher.Consumers, consumer => consumer.Name == _subscription.Name && consumer.State == ConsumerState.Open);
            Assert.Equal(DispatcherState.DS_STOPPED, _dispatcher.State);
            Assert.Empty(_dispatcher.Consumers);
        }
        
        public void Dispose()
        {
            if (_dispatcher?.State == DispatcherState.DS_RUNNING)
                _dispatcher.End().Wait();
        }
    }
}
