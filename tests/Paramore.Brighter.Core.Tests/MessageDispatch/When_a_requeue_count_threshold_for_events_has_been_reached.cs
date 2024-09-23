﻿#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Xunit;
using Paramore.Brighter.ServiceActivator;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;

namespace Paramore.Brighter.Core.Tests.MessageDispatch
{
    public class MessagePumpEventRequeueCountThresholdTests
    {
        private const string Channel = "MyChannel";
        private readonly RoutingKey _routingKey = new("MyTopic");
        private readonly InternalBus _bus = new();
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly IAmAMessagePump _messagePump;
        private readonly Channel _channel;
        private readonly SpyRequeueCommandProcessor _commandProcessor;

        public MessagePumpEventRequeueCountThresholdTests()
        {
            _commandProcessor = new SpyRequeueCommandProcessor();
            var provider = new CommandProcessorProvider(_commandProcessor);
            _channel = new Channel(new(Channel), _routingKey, new InMemoryMessageConsumer(_routingKey, _bus, _timeProvider, TimeSpan.FromMilliseconds(1000)));
            var messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory(_ => new MyEventMessageMapper()),
                null);
            messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();
             
            _messagePump = new MessagePumpBlocking<MyEvent>(provider, messageMapperRegistry, new EmptyMessageTransformerFactory(), new InMemoryRequestContextFactory(), _channel) 
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

            _commandProcessor.Commands[0].Should().Be(CommandType.Publish);
            _commandProcessor.PublishCount.Should().Be(6);
            
            Assert.Empty(_bus.Stream(_routingKey));
            
            //TODO: How do we assert that the channel was closed? Observability?
        }
    }
}
