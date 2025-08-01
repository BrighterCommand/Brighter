using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Proactor
{
   [Collection("CommandProcessor")]
    public class DispatcherRestartConnectionTestsAsync : IDisposable
    {
        private const string ChannelName = "fakeChannel";
        private readonly Dispatcher _dispatcher;
        private readonly Publication _publication;
        private readonly RoutingKey _routingKey = new("fakekey");
        private readonly ChannelName _channelName = new(ChannelName);
        private readonly InternalBus _bus = new();
        private readonly FakeTimeProvider _timeProvider = new();

        public DispatcherRestartConnectionTestsAsync()
        {
            IAmACommandProcessor commandProcessor = new SpyCommandProcessor();

            var messageMapperRegistry = new MessageMapperRegistry(
                null,
                new SimpleMessageMapperFactoryAsync((_) => new MyEventMessageMapperAsync()));
            messageMapperRegistry.RegisterAsync <MyEvent, MyEventMessageMapperAsync>();

            Subscription subscription = new Subscription<MyEvent>(
                new SubscriptionName("test"),
                noOfPerformers: 1,
                timeOut: TimeSpan.FromMilliseconds(100),
                channelFactory: new InMemoryChannelFactory(_bus, _timeProvider),
                channelName: _channelName,
                messagePumpType: MessagePumpType.Proactor,
                routingKey: _routingKey
            );
            
            Subscription newSubscription = new Subscription<MyEvent>(
                new SubscriptionName("newTest"), 
                noOfPerformers: 1, timeOut: TimeSpan.FromMilliseconds(100), 
                channelFactory: new InMemoryChannelFactory(_bus, _timeProvider), 
                channelName: _channelName, 
                messagePumpType: MessagePumpType.Proactor,
                routingKey: _routingKey
            );
            
            _publication = new Publication{Topic = subscription.RoutingKey};
            
            _dispatcher = new Dispatcher(
                commandProcessor, 
                new List<Subscription> { subscription, newSubscription }, 
                messageMapperRegistryAsync: messageMapperRegistry)
            ;

            var @event = new MyEvent();
            var message = new MyEventMessageMapperAsync()
                .MapToMessageAsync(@event, _publication )
                .GetAwaiter()
                .GetResult();
           
            _bus.Enqueue(message);


            Assert.Equal(DispatcherState.DS_AWAITING, _dispatcher.State);
            _dispatcher.Receive();
            Task.Delay(250).Wait();
            _dispatcher.Shut(subscription.Name);
            _dispatcher.Shut(newSubscription.Name);
            Task.Delay(1000).Wait();
            Assert.Empty(_dispatcher.Consumers);
            
        }

        [Fact]
        public async Task When_A_Message_Dispatcher_Restarts_A_Connection_After_All_Connections_Have_Stopped()
        {
            _dispatcher.Open(new SubscriptionName("newTest"));
            var @event = new MyEvent();
            var message = await new MyEventMessageMapperAsync().MapToMessageAsync(@event, _publication);
            _bus.Enqueue(message);
            
            await Task.Delay(1000);
            _timeProvider.Advance(TimeSpan.FromSeconds(2)); //This will trigger requeue of not acked/rejected messages
            
            Assert.Empty(_bus.Stream(_routingKey));
            Assert.Equal(DispatcherState.DS_RUNNING, _dispatcher.State);
            Assert.Single(_dispatcher.Consumers);
            Assert.Equal(2, _dispatcher.Subscriptions.Count());
        }

        public void Dispose()
        {
            if (_dispatcher?.State == DispatcherState.DS_RUNNING)
                _dispatcher.End().Wait();
        }
    }
}
