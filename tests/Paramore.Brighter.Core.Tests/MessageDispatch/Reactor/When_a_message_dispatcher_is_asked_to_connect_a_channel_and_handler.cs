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
    public class MessageDispatcherRoutingTests : IDisposable
    {
        private readonly Dispatcher _dispatcher;
        private readonly SpyCommandProcessor _commandProcessor;
        private readonly RoutingKey _routingKey = new("myTopic");
        private readonly InternalBus _bus = new();
        private readonly FakeTimeProvider _timeProvider = new();

        public MessageDispatcherRoutingTests()
        {
            _commandProcessor = new SpyCommandProcessor();

            var messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory((_) => new MyEventMessageMapper()),
                null);
            messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

            var subscription = new Subscription<MyEvent>(
                new SubscriptionName("test"), 
                noOfPerformers: 1, 
                timeOut: TimeSpan.FromMilliseconds(1000), 
                channelFactory: new InMemoryChannelFactory(_bus, _timeProvider),
                channelName: new ChannelName("myChannel"), 
                messagePumpType: MessagePumpType.Reactor,
                routingKey: _routingKey
            );
            
            _dispatcher = new Dispatcher(
                _commandProcessor, 
                new List<Subscription> { subscription },
                messageMapperRegistry,
                requestContextFactory: new InMemoryRequestContextFactory()
            );

            var @event = new MyEvent();
            var message = new MyEventMessageMapper().MapToMessage(@event, new Publication{Topic = _routingKey});
            _bus.Enqueue(message);

            Assert.Equal(DispatcherState.DS_AWAITING, _dispatcher.State);
            _dispatcher.Receive();
            
        }

#pragma warning disable xUnit1031
        [Fact]
        public void When_A_Message_Dispatcher_Is_Asked_To_Connect_A_Channel_And_Handler()
        {
            Task.Delay(1000).Wait();
            _timeProvider.Advance(TimeSpan.FromSeconds(2)); //This will trigger requeue of not acked/rejected messages
            
            _dispatcher.End().Wait();
            
            Assert.Empty(_bus.Stream(_routingKey));
            Assert.Equal(DispatcherState.DS_STOPPED, _dispatcher.State);
            Assert.NotNull(_commandProcessor.Observe<MyEvent>());
            Assert.Contains(_commandProcessor.Commands, ctype => ctype == CommandType.Publish);
        }
#pragma warning restore xUnit1031
        
        public void Dispose()
        {
            if (_dispatcher?.State == DispatcherState.DS_RUNNING)
                _dispatcher.End().Wait();
        }
    }
}
