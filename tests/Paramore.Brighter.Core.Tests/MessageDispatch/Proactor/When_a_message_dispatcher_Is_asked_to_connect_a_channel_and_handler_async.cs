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
    public class MessageDispatcherRoutingAsyncTests
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
            var messageMapperRegistry = new MessageMapperRegistry(null, new SimpleMessageMapperFactoryAsync((_) => new MyEventMessageMapperAsync()));
            messageMapperRegistry.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();
            var subscription = new Subscription<MyEvent>(new SubscriptionName("test"), noOfPerformers: 1, timeOut: TimeSpan.FromMilliseconds(1000), channelFactory: new InMemoryChannelFactory(_bus, _timeProvider), channelName: new ChannelName(ChannelName), routingKey: _routingKey, messagePumpType: MessagePumpType.Proactor);
            _dispatcher = new Dispatcher(_commandProcessor, new List<Subscription> { subscription }, null, messageMapperRegistry, requestContextFactory: new InMemoryRequestContextFactory());
        }

        [Before(Test)]
        public async Task Setup()
        {
            var @event = new MyEvent
            {
                Data = 4
            };
            var message = await new MyEventMessageMapperAsync().MapToMessageAsync(@event, new() { Topic = _routingKey });
            _bus.Enqueue(message);
            await Assert.That(_dispatcher.State).IsEqualTo(DispatcherState.DS_AWAITING);
            _dispatcher.Receive();
        }

        [Test]
        public async Task When_a_message_dispatcher_is_asked_to_connect_a_channel_and_handler_async()
        {
            await Assert.That(() => _bus.Stream(_routingKey).Any())
                .Eventually(src => src.IsFalse(), TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(250));
            _timeProvider.Advance(TimeSpan.FromSeconds(2)); //This will trigger requeue of not acked/rejected messages
            await _dispatcher.End();
            await Assert.That(_dispatcher.State).IsEqualTo(DispatcherState.DS_STOPPED);
            await Assert.That(_commandProcessor.Observe<MyEvent>()).IsNotNull();
            await Assert.That(_commandProcessor.Commands).Contains(CommandType.PublishAsync);
            await Assert.That(_bus.Stream(_routingKey)).IsEmpty();
        }

        [After(Test)]
        public async Task Dispose()
        {
            if (_dispatcher?.State == DispatcherState.DS_RUNNING)
                await _dispatcher.End();
        }
    }
}