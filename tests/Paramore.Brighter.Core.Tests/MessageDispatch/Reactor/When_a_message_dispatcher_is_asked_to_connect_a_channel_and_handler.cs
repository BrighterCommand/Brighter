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
    public class MessageDispatcherRoutingTests
    {
        private readonly Dispatcher _dispatcher;
        private readonly SpyCommandProcessor _commandProcessor;
        private readonly RoutingKey _routingKey = new("myTopic");
        private readonly InternalBus _bus = new();
        private readonly FakeTimeProvider _timeProvider = new();
        public MessageDispatcherRoutingTests()
        {
            _commandProcessor = new SpyCommandProcessor();
            var messageMapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory((_) => new MyEventMessageMapper()), null);
            messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();
            var subscription = new Subscription<MyEvent>(new SubscriptionName("test"), noOfPerformers: 1, timeOut: TimeSpan.FromMilliseconds(1000), channelFactory: new InMemoryChannelFactory(_bus, _timeProvider), channelName: new ChannelName("myChannel"), messagePumpType: MessagePumpType.Reactor, routingKey: _routingKey);
            _dispatcher = new Dispatcher(_commandProcessor, new List<Subscription> { subscription }, messageMapperRegistry, requestContextFactory: new InMemoryRequestContextFactory());
            var @event = new MyEvent();
            var message = new MyEventMessageMapper().MapToMessage(@event, new Publication { Topic = _routingKey });
            _bus.Enqueue(message);
        }

        [Before(Test)]
        public async Task Setup()
        {
            await Assert.That(_dispatcher.State).IsEqualTo(DispatcherState.DS_AWAITING);
            _dispatcher.Receive();
        }

        [Test]
        public async Task When_A_Message_Dispatcher_Is_Asked_To_Connect_A_Channel_And_Handler()
        {
            await Assert.That(() => _bus.Stream(_routingKey).Any())
                .Eventually(src => src.IsFalse(), TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(250));
            _timeProvider.Advance(TimeSpan.FromSeconds(2)); //This will trigger requeue of not acked/rejected messages
            await _dispatcher.End();
            await Assert.That(_bus.Stream(_routingKey)).IsEmpty();
            await Assert.That(_dispatcher.State).IsEqualTo(DispatcherState.DS_STOPPED);
            await Assert.That(_commandProcessor.Observe<MyEvent>()).IsNotNull();
            await Assert.That(_commandProcessor.Commands).Contains(ctype => ctype == CommandType.Publish);
        }

        [After(Test)]
        public async Task Dispose()
        {
            if (_dispatcher?.State == DispatcherState.DS_RUNNING)
                await _dispatcher.End();
        }
    }
}