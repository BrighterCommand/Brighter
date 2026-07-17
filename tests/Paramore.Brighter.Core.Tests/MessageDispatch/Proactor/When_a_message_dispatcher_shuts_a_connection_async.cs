using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.Testing;
using Paramore.Brighter.ServiceActivator;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Proactor
{
    public class MessageDispatcherShutConnectionTests
    {
        private const string Topic = "fakekey";
        private const string ChannelName = "fakeChannel";
        private readonly Dispatcher _dispatcher;
        private readonly Subscription _subscription;
        private readonly RoutingKey _routingKey = new(Topic);
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly InternalBus _bus;
        public MessageDispatcherShutConnectionTests()
        {
            _bus = new InternalBus();
            IAmACommandProcessor commandProcessor = new SpyCommandProcessor();
            var messageMapperRegistry = new MessageMapperRegistry(null, new SimpleMessageMapperFactoryAsync((_) => new MyEventMessageMapperAsync()));
            messageMapperRegistry.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();
            _subscription = new Subscription<MyEvent>(new SubscriptionName("test"), noOfPerformers: 3, timeOut: TimeSpan.FromMilliseconds(1000), channelFactory: new InMemoryChannelFactory(_bus, _timeProvider), channelName: new ChannelName(ChannelName), messagePumpType: MessagePumpType.Proactor, routingKey: _routingKey);
            _dispatcher = new Dispatcher(commandProcessor, new List<Subscription> { _subscription }, messageMapperRegistryAsync: messageMapperRegistry);
        }

        [Before(Test)]
        public async Task Setup()
        {
            var @event = new MyEvent();
            var message = await new MyEventMessageMapperAsync().MapToMessageAsync(@event, new Publication { Topic = _subscription.RoutingKey });
            for (var i = 0; i < 6; i++)
                _bus.Enqueue(message);
            await Assert.That(_dispatcher.State).IsEqualTo(DispatcherState.DS_AWAITING);
            _dispatcher.Receive();
        }

        [Test]
        public async Task When_A_Message_Dispatcher_Shuts_A_Connection()
        {
            await Assert.That(() => _dispatcher.Consumers.Any())
                .Eventually(src => src.IsTrue(), TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(250));
            _dispatcher.Shut(_subscription);
            await _dispatcher.End();
            await Assert.That((_dispatcher.Consumers).Any(consumer => consumer.Name == _subscription.Name && consumer.State == ConsumerState.Open)).IsFalse();
            await Assert.That(_dispatcher.State).IsEqualTo(DispatcherState.DS_STOPPED);
            await Assert.That(_dispatcher.Consumers).IsEmpty();
        }

        [After(Test)]
        public async Task Dispose()
        {
            if (_dispatcher?.State == DispatcherState.DS_RUNNING)
                await _dispatcher.End();
        }
    }
}
