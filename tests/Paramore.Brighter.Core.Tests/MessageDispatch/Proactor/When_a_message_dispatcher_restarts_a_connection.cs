using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Proactor
{
    [Collection("CommandProcessor")]
    public class MessageDispatcherResetConnectionAsync : IDisposable
    {
        private readonly Dispatcher _dispatcher;
        private readonly Subscription _subscription;
        private readonly Publication _publication;
        private readonly InternalBus _bus = new();
        private readonly RoutingKey _routingKey = new("myTopic");
        private readonly FakeTimeProvider _timeProvider = new();

        public MessageDispatcherResetConnectionAsync()
        {
            IAmACommandProcessor commandProcessor = new SpyCommandProcessor();

            var messageMapperRegistry = new MessageMapperRegistry(
                null,
                new SimpleMessageMapperFactoryAsync((_) => new MyEventMessageMapperAsync()));
            messageMapperRegistry.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();

            _subscription = new Subscription<MyEvent>(
                new SubscriptionName("test"), 
                noOfPerformers: 1, 
                timeOut: TimeSpan.FromMilliseconds(1000), 
                channelFactory: new InMemoryChannelFactory(_bus, _timeProvider), 
                channelName: new ChannelName("myChannel"), 
                messagePumpType: MessagePumpType.Proactor,
                routingKey: _routingKey
            );
            
            _publication = new Publication{Topic = _subscription.RoutingKey, RequestType = typeof(MyEvent)};
            
            _dispatcher = new Dispatcher(commandProcessor, new List<Subscription> { _subscription }, messageMapperRegistryAsync:messageMapperRegistry);

            var @event = new MyEvent();
            var message = new MyEventMessageMapperAsync()
                .MapToMessageAsync(@event, _publication)
                .GetAwaiter()
                .GetResult();
            
            _bus.Enqueue(message);

            Assert.Equal(DispatcherState.DS_AWAITING, _dispatcher.State);
            _dispatcher.Receive();
            Task.Delay(1000).Wait();
            _dispatcher.Shut(_subscription);
            
        }
        		 
#pragma warning disable xUnit1031
        [Fact]
        public async Task When_A_Message_Dispatcher_Restarts_A_Connection()
        {
            _dispatcher.Open(_subscription);

            var @event = new MyEvent();
            var message = await new MyEventMessageMapperAsync().MapToMessageAsync(@event, _publication);
            _bus.Enqueue(message);

            await Task.Delay(1000);
            
            _timeProvider.Advance(TimeSpan.FromSeconds(2)); //This will trigger requeue of not acked/rejected messages

            await _dispatcher.End();

            Assert.Empty(_bus.Stream(_routingKey));
            Assert.Equal(DispatcherState.DS_STOPPED, _dispatcher.State);
        }
#pragma warning restore xUnit1031
        
        public void Dispose()
        {
            if (_dispatcher?.State == DispatcherState.DS_RUNNING)
                _dispatcher.End().Wait();
        }
    }
}
