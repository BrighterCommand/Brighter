using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.Testing;
using Paramore.Brighter.ServiceActivator;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Reactor
{
    public class DispatcherRestartConnectionTests
    {
        private const string ChannelName = "fakeChannel";
        private readonly Dispatcher _dispatcher;
        private readonly Publication _publication;
        private readonly RoutingKey _routingKey = new("fakekey");
        private readonly ChannelName _channelName = new(ChannelName);
        private readonly InternalBus _bus = new();
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly Subscription _subscription;
        private readonly Subscription _newSubscription;
        public DispatcherRestartConnectionTests()
        {
            IAmACommandProcessor commandProcessor = new SpyCommandProcessor();
            var messageMapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory((_) => new MyEventMessageMapper()), null);
            messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();
            _subscription = new Subscription<MyEvent>(new SubscriptionName("test"), noOfPerformers: 1, timeOut: TimeSpan.FromMilliseconds(100), channelFactory: new InMemoryChannelFactory(_bus, _timeProvider), channelName: _channelName, messagePumpType: MessagePumpType.Reactor, routingKey: _routingKey);
            _newSubscription = new Subscription<MyEvent>(new SubscriptionName("newTest"), noOfPerformers: 1, timeOut: TimeSpan.FromMilliseconds(100), channelFactory: new InMemoryChannelFactory(_bus, _timeProvider), channelName: _channelName, messagePumpType: MessagePumpType.Reactor, routingKey: _routingKey);
            _publication = new Publication
            {
                Topic = _subscription.RoutingKey
            };
            _dispatcher = new Dispatcher(commandProcessor, new List<Subscription> { _subscription, _newSubscription }, messageMapperRegistry);
            var @event = new MyEvent();
            var message = new MyEventMessageMapper().MapToMessage(@event, _publication);
            _bus.Enqueue(message);
        }

        [Before(Test)]
        public async Task Setup()
        {
            await Assert.That(_dispatcher.State).IsEqualTo(DispatcherState.DS_AWAITING);
            _dispatcher.Receive();
            await Assert.That(() => _bus.Stream(_routingKey).Any())
                .Eventually(src => src.IsFalse(), TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(250));
            _dispatcher.Shut(_subscription.Name);
            _dispatcher.Shut(_newSubscription.Name);
            await Assert.That(() => _dispatcher.Consumers.Any())
                .Eventually(src => src.IsFalse(), TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(250));
        }

        [Test]
        public async Task When_A_Message_Dispatcher_Restarts_A_Connection_After_All_Connections_Have_Stopped()
        {
            _dispatcher.Open(new SubscriptionName("newTest"));
            var @event = new MyEvent();
            var message = new MyEventMessageMapper().MapToMessage(@event, _publication);
            _bus.Enqueue(message);
            await Assert.That(() => _bus.Stream(_routingKey).Any())
                .Eventually(src => src.IsFalse(), TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(250));
            _timeProvider.Advance(TimeSpan.FromSeconds(2)); //This will trigger requeue of not acked/rejected messages
            await Assert.That(_dispatcher.State).IsEqualTo(DispatcherState.DS_RUNNING);
            await Assert.That(_dispatcher.Consumers).HasSingleItem();
            await Assert.That(_dispatcher.Subscriptions.Count()).IsEqualTo(2);
        }

        [After(Test)]
        public async Task Dispose()
        {
            if (_dispatcher?.State == DispatcherState.DS_RUNNING)
                await _dispatcher.End();
        }
    }
}