using System;
using System.Collections.Generic;
using System.Linq;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Reactor
{

    public class MessageDispatcherMultiplePerformerTests
    {
        private const string Topic = "myTopic";
        private const string ChannelName = "myChannel";
        private readonly Dispatcher _dispatcher;
        private readonly InternalBus _bus;

        public MessageDispatcherMultiplePerformerTests()
        {
            var routingKey = new RoutingKey(Topic);
            _bus = new InternalBus();
            var consumer = new InMemoryMessageConsumer(routingKey, _bus, TimeProvider.System, TimeSpan.FromMilliseconds(1000));
            
            IAmAChannelSync channel = new Channel(new (ChannelName), new(Topic), consumer, 6);
            IAmACommandProcessor commandProcessor = new SpyCommandProcessor();

            var messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory((_) => new MyEventMessageMapper()),
                null);
            messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

            var connection = new Subscription<MyEvent>(
                new SubscriptionName("test"), 
                noOfPerformers: 3, 
                timeOut: TimeSpan.FromMilliseconds(100), 
                channelFactory: new InMemoryChannelFactory(_bus, TimeProvider.System), 
                channelName: new ChannelName("fakeChannel"), 
                messagePumpType: MessagePumpType.Reactor,
                routingKey: routingKey
            );
            _dispatcher = new Dispatcher(commandProcessor, new List<Subscription> { connection }, messageMapperRegistry);

            var @event = new MyEvent();
            var message = new MyEventMessageMapper().MapToMessage(@event, new Publication{Topic = connection.RoutingKey});
            for (var i = 0; i < 6; i++)
                channel.Enqueue(message);
            
            Assert.Equal(DispatcherState.DS_AWAITING, _dispatcher.State);
            _dispatcher.Receive();
        }

#pragma warning disable xUnit1031
        [Fact]
        public void WhenAMessageDispatcherStartsMultiplePerformers()
        {
            {
                Assert.Equal(DispatcherState.DS_RUNNING, _dispatcher.State);
                Assert.Equal(3, _dispatcher.Consumers.Count());

                _dispatcher.End().Wait();

                Assert.Empty(_bus.Stream(new RoutingKey(Topic)));
                Assert.Equal(DispatcherState.DS_STOPPED, _dispatcher.State);
            }
        }
#pragma warning restore xUnit1031
    }
}
