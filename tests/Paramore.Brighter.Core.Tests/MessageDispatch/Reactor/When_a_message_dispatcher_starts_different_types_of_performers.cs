using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.Testing;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Reactor
{
    public class MessageDispatcherMultipleConnectionTests
    {
        private readonly Dispatcher _dispatcher;
        private int _numberOfConsumers;
        private readonly InternalBus _bus = new();
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly RoutingKey _commandRoutingKey = new("myCommand");
        private readonly RoutingKey _eventRoutingKey = new("myEvent");
        public MessageDispatcherMultipleConnectionTests()
        {
            var commandProcessor = new SpyCommandProcessor();
            var container = new ServiceCollection();
            container.AddTransient<MyEventMessageMapper>();
            container.AddTransient<MyCommandMessageMapper>();
            var messageMapperRegistry = new MessageMapperRegistry(new ServiceProviderMapperFactory(container.BuildServiceProvider()), null);
            messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();
            messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();
            var myEventConnection = new Subscription<MyEvent>(new SubscriptionName("test"), noOfPerformers: 1, timeOut: TimeSpan.FromMilliseconds(1000), channelFactory: new InMemoryChannelFactory(_bus, _timeProvider), messagePumpType: MessagePumpType.Reactor, channelName: new ChannelName("fakeEventChannel"), routingKey: _eventRoutingKey);
            var myCommandConnection = new Subscription<MyCommand>(new SubscriptionName("anothertest"), noOfPerformers: 1, timeOut: TimeSpan.FromMilliseconds(1000), channelFactory: new InMemoryChannelFactory(_bus, _timeProvider), channelName: new ChannelName("fakeCommandChannel"), messagePumpType: MessagePumpType.Reactor, routingKey: _commandRoutingKey);
            _dispatcher = new Dispatcher(commandProcessor, new List<Subscription> { myEventConnection, myCommandConnection }, messageMapperRegistry);
            var @event = new MyEvent();
            var eventMessage = new MyEventMessageMapper().MapToMessage(@event, new Publication { Topic = _eventRoutingKey });
            _bus.Enqueue(eventMessage);
            var command = new MyCommand();
            var commandMessage = new MyCommandMessageMapper().MapToMessage(command, new Publication { Topic = _commandRoutingKey });
            _bus.Enqueue(commandMessage);
        }

        [Before(Test)]
        public async Task Setup()
        {
            await Assert.That(_dispatcher.State).IsEqualTo(DispatcherState.DS_AWAITING);
            _dispatcher.Receive();
        }

        [Test]
        public async Task When_A_Message_Dispatcher_Starts_Different_Types_Of_Performers()
        {
            await Assert.That(() => _dispatcher.Consumers.Count())
                .Eventually(src => src.IsEqualTo(2), TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(250));
            _numberOfConsumers = _dispatcher.Consumers.Count();
            _timeProvider.Advance(TimeSpan.FromSeconds(2)); //This will trigger requeue of not acked/rejected messages
            await _dispatcher.End();
            await Assert.That(_bus.Stream(_eventRoutingKey)).IsEmpty();
            await Assert.That(_bus.Stream(_commandRoutingKey)).IsEmpty();
            await Assert.That(_dispatcher.State).IsEqualTo(DispatcherState.DS_STOPPED);
            await Assert.That(_dispatcher.Consumers).IsEmpty();
            await Assert.That(_numberOfConsumers).IsEqualTo(2);
        }

        [After(Test)]
        public async Task Dispose()
        {
            if (_dispatcher?.State == DispatcherState.DS_RUNNING)
                await _dispatcher.End();
        }
    }
}