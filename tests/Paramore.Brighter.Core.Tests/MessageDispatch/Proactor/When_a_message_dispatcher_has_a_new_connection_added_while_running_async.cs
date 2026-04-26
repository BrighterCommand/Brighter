using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.Testing;
using Paramore.Brighter.ServiceActivator;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Proactor
{
    public class DispatcherAddNewConnectionTestsAsync
    {
        private readonly Dispatcher _dispatcher;
        private readonly Subscription _newSubscription;
        private readonly InternalBus _bus;
        private readonly RoutingKey _routingKey = new("MyEvent");
        private readonly RoutingKey _routingKeyTwo = new("OtherEvent");
        public DispatcherAddNewConnectionTestsAsync()
        {
            _bus = new InternalBus();
            IAmACommandProcessor commandProcessor = new SpyCommandProcessor();
            var messageMapperRegistry = new MessageMapperRegistry(null, new SimpleMessageMapperFactoryAsync((_) => new MyEventMessageMapperAsync()));
            messageMapperRegistry.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();
            Subscription subscription = new Subscription<MyEvent>(new SubscriptionName("test"), noOfPerformers: 1, timeOut: TimeSpan.FromMilliseconds(1000), channelFactory: new InMemoryChannelFactory(_bus, TimeProvider.System), channelName: new ChannelName("fakeChannel"), messagePumpType: MessagePumpType.Proactor, routingKey: _routingKey);
            _newSubscription = new Subscription<MyEvent>(new SubscriptionName("newTest"), noOfPerformers: 1, timeOut: TimeSpan.FromMilliseconds(1000), channelFactory: new InMemoryChannelFactory(_bus, TimeProvider.System), channelName: new ChannelName("fakeChannelTwo"), messagePumpType: MessagePumpType.Proactor, routingKey: _routingKeyTwo);
            _dispatcher = new Dispatcher(commandProcessor, new List<Subscription> { subscription }, messageMapperRegistryAsync: messageMapperRegistry);
        }

        [Before(Test)]
        public async Task Setup()
        {
            var @event = new MyEvent();
            var message = await new MyEventMessageMapperAsync().MapToMessageAsync(@event, new Publication { Topic = _routingKey });
            _bus.Enqueue(message);
            await Assert.That(_dispatcher.State).IsEqualTo(DispatcherState.DS_AWAITING);
            _dispatcher.Receive();
        }

        [Test]
        public async Task When_A_Message_Dispatcher_Has_A_New_Connection_Added_While_Running()
        {
            _dispatcher.Open(_newSubscription);
            var @event = new MyEvent();
            var message = await new MyEventMessageMapperAsync().MapToMessageAsync(@event, new Publication { Topic = _routingKeyTwo });
            _bus.Enqueue(message);
            await Assert.That(() => _bus.Stream(_routingKey).Any())
                .Eventually(src => src.IsFalse(), TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(250));
            await Assert.That(_dispatcher.State).IsEqualTo(DispatcherState.DS_RUNNING);
            await Assert.That(_dispatcher.Consumers.Count()).IsEqualTo(2);
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