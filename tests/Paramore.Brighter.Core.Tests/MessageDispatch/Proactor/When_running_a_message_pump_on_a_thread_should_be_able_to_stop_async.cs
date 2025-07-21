using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.ServiceActivator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Proactor
{
    public class PerformerCanStopTestsAsync
    {
        private const string Channel = "MyChannel";
        private readonly RoutingKey _routingKey = new("MyTopic");
        private readonly InternalBus _bus = new();
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly Task _performerTask;

        public PerformerCanStopTestsAsync()
        {
            SpyCommandProcessor commandProcessor = new();
            ChannelAsync channel = new(
                new(Channel), _routingKey, 
                new InMemoryMessageConsumer(_routingKey, _bus, _timeProvider, TimeSpan.FromMilliseconds(1000))
            );
            
            var messageMapperRegistry = new MessageMapperRegistry(
                null,
                new SimpleMessageMapperFactoryAsync(_ => new MyEventMessageMapperAsync()));
            messageMapperRegistry.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();
            
            var messagePump = new ServiceActivator.Proactor(commandProcessor, (message) => typeof(MyEvent), messageMapperRegistry, 
                new EmptyMessageTransformerFactoryAsync(), new InMemoryRequestContextFactory(), channel);
            messagePump.Channel = channel;
            messagePump.TimeOut = TimeSpan.FromMilliseconds(5000);

            var @event = new MyEvent();
            var message = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_EVENT), 
                new MessageBody(JsonSerializer.Serialize(@event, JsonSerialisationOptions.Options))
            );
            channel.Enqueue(message);
            
            Performer performer = new(channel, messagePump);
            _performerTask = performer.Run();
            performer.Stop(_routingKey);
        }
        
#pragma warning disable xUnit1031
        [Fact]
        public void When_Running_A_Message_Pump_On_A_Thread_Should_Be_Able_To_Stop()
        {
            _performerTask.Wait();

            Assert.True(_performerTask.IsCompleted);
            Assert.False(_performerTask.IsFaulted);
            Assert.False(_performerTask.IsCanceled);
            Assert.Empty(_bus.Stream(_routingKey));
        }
#pragma warning restore xUnit1031
    }
}
