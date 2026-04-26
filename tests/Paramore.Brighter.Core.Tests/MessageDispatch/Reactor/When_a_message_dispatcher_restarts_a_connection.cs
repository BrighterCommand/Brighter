using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.Testing;
using Paramore.Brighter.ServiceActivator;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Reactor
{
    public class MessageDispatcherResetConnection
    {
        private readonly Dispatcher _dispatcher;
        private readonly Subscription _subscription;
        private readonly Publication _publication;
        private readonly InternalBus _bus = new();
        private readonly RoutingKey _routingKey = new("myTopic");
        private readonly FakeTimeProvider _timeProvider = new();
        public MessageDispatcherResetConnection()
        {
            IAmACommandProcessor commandProcessor = new SpyCommandProcessor();
            var messageMapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory((_) => new MyEventMessageMapper()), null);
            messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();
            _subscription = new Subscription<MyEvent>(new SubscriptionName("test"), noOfPerformers: 1, timeOut: TimeSpan.FromMilliseconds(1000), channelFactory: new InMemoryChannelFactory(_bus, _timeProvider), channelName: new ChannelName("myChannel"), messagePumpType: MessagePumpType.Reactor, routingKey: _routingKey);
            _publication = new Publication
            {
                Topic = _subscription.RoutingKey,
                RequestType = typeof(MyEvent)
            };
            _dispatcher = new Dispatcher(commandProcessor, new List<Subscription> { _subscription }, messageMapperRegistry);
            var @event = new MyEvent();
            var message = new MyEventMessageMapper().MapToMessage(@event, _publication);
            _bus.Enqueue(message);
        }

        [Before(Test)]
        public async Task Setup()
        {
            await Assert.That(_dispatcher.State).IsEqualTo(DispatcherState.DS_AWAITING);
            _dispatcher.Receive();
            await Task.Delay(1000);
            _dispatcher.Shut(_subscription);
        }

        [Test]
        public async Task When_A_Message_Dispatcher_Restarts_A_Connection()
        {
            _dispatcher.Open(_subscription);
            var @event = new MyEvent();
            var message = new MyEventMessageMapper().MapToMessage(@event, _publication);
            _bus.Enqueue(message);
            await Task.Delay(1000);
            _timeProvider.Advance(TimeSpan.FromSeconds(2)); //This will trigger requeue of not acked/rejected messages
            await _dispatcher.End();
            await Assert.That(_bus.Stream(_routingKey)).IsEmpty();
            await Assert.That(_dispatcher.State).IsEqualTo(DispatcherState.DS_STOPPED);
        }

        [After(Test)]
        public async Task Dispose()
        {
            if (_dispatcher?.State == DispatcherState.DS_RUNNING)
                await _dispatcher.End();
        }
    }
}