﻿using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Proactor
{
    public class MessagePumpEventRequeueCountThresholdTestsAsync
    {
        private const string Channel = "MyChannel";
        private readonly RoutingKey _routingKey = new("MyTopic");
        private readonly InternalBus _bus = new();
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly IAmAMessagePump _messagePump;
        private readonly ChannelAsync _channel;
        private readonly SpyRequeueCommandProcessor _commandProcessor;

        public MessagePumpEventRequeueCountThresholdTestsAsync()
        {
            _commandProcessor = new SpyRequeueCommandProcessor();
            _channel = new ChannelAsync(new(Channel), _routingKey, new InMemoryMessageConsumer(_routingKey, _bus, _timeProvider, TimeSpan.FromMilliseconds(1000)));
           
            var messageMapperRegistry = new MessageMapperRegistry(
                null,
                new SimpleMessageMapperFactoryAsync(_ => new MyEventMessageMapperAsync()));
            messageMapperRegistry.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();
             
            _messagePump = new Proactor<MyEvent>(_commandProcessor, messageMapperRegistry, new EmptyMessageTransformerFactoryAsync(), new InMemoryRequestContextFactory(), _channel) 
                { Channel = _channel, TimeOut = TimeSpan.FromMilliseconds(5000), RequeueCount = 3 };

            var message1 = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_EVENT), 
                new MessageBody(JsonSerializer.Serialize((MyEvent)new(), JsonSerialisationOptions.Options))
            );
            var message2 = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_EVENT), 
                new MessageBody(JsonSerializer.Serialize((MyEvent)new(), JsonSerialisationOptions.Options))
            );
            _bus.Enqueue(message1);
            _bus.Enqueue(message2);
        }

        [Fact]
        public async Task When_A_Requeue_Count_Threshold_For_Events_Has_Been_Reached()
        {
            var task = Task.Factory.StartNew(() => _messagePump.Run(), TaskCreationOptions.LongRunning);
            await Task.Delay(1000);
            
            _timeProvider.Advance(TimeSpan.FromSeconds(2)); //This will trigger requeue of not acked/rejected messages

            var quitMessage = MessageFactory.CreateQuitMessage(_routingKey);
            _channel.Enqueue(quitMessage);

            await Task.WhenAll(task);

            Assert.Equal(CommandType.PublishAsync, _commandProcessor.Commands[0]);
            Assert.Equal(6, _commandProcessor.PublishCount);

            Assert.Empty(_bus.Stream(_routingKey));
            
            //TODO: How do we assert that the channel was closed? Observability?
        }
    }
}
