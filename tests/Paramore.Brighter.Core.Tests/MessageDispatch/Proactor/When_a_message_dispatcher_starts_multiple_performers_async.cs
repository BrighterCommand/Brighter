using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Proactor
{

    public class MessageDispatcherMultiplePerformerTestsAsync
    {
        private const string Topic = "myTopic";
        private const string ChannelName = "myChannel";
        private readonly Dispatcher _dispatcher;
        private readonly InternalBus _bus;

        public MessageDispatcherMultiplePerformerTestsAsync()
        {
            var routingKey = new RoutingKey(Topic);
            _bus = new InternalBus();
            var consumer = new InMemoryMessageConsumer(routingKey, _bus, TimeProvider.System, TimeSpan.FromMilliseconds(1000));
            
            IAmAChannelSync channel = new Channel(new (ChannelName), new(Topic), consumer, 6);
            IAmACommandProcessor commandProcessor = new SpyCommandProcessor();

            var messageMapperRegistry = new MessageMapperRegistry(
                null,
                new SimpleMessageMapperFactoryAsync((_) => new MyEventMessageMapperAsync()));
            messageMapperRegistry.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();

            var connection = new Subscription<MyEvent>(
                new SubscriptionName("test"), 
                noOfPerformers: 3, 
                timeOut: TimeSpan.FromMilliseconds(100), 
                channelFactory: new InMemoryChannelFactory(_bus, TimeProvider.System), 
                channelName: new ChannelName("fakeChannel"), 
                messagePumpType: MessagePumpType.Proactor,
                routingKey: routingKey
            );
            _dispatcher = new Dispatcher(commandProcessor, new List<Subscription> { connection }, messageMapperRegistryAsync: messageMapperRegistry);

            var @event = new MyEvent();
            var message = new MyEventMessageMapperAsync().MapToMessageAsync(@event, new Publication{Topic = connection.RoutingKey})
                .GetAwaiter()
                .GetResult();
            
            for (var i = 0; i < 6; i++)
                channel.Enqueue(message);
            
            Assert.Equal(DispatcherState.DS_AWAITING, _dispatcher.State);
            _dispatcher.Receive();
        }

        [Fact]
        public async Task WhenAMessageDispatcherStartsMultiplePerformers()
        {
                Assert.Equal(DispatcherState.DS_RUNNING, _dispatcher.State);
                Assert.Equal(3, _dispatcher.Consumers.Count());

                await _dispatcher.End();

                Assert.Empty(_bus.Stream(new RoutingKey(Topic)));
                Assert.Equal(DispatcherState.DS_STOPPED, _dispatcher.State);
        }
    }
}
