#region Licence
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
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Xunit;
using Paramore.Brighter.ServiceActivator;

namespace Paramore.Brighter.Core.Tests.MessageDispatch
{
    public class MessagePumpUnacceptableMessageLimitBreachedAsyncTests
    {
        private const string Topic = "MyTopic";
        private readonly IAmAMessagePump _messagePump;
        private readonly InternalBus _bus = new();
        private readonly RoutingKey _routingKey = new(Topic);
        private readonly FakeTimeProvider _timeProvider = new();

        public MessagePumpUnacceptableMessageLimitBreachedAsyncTests()
        {
            SpyRequeueCommandProcessor commandProcessor = new();
            var provider = new CommandProcessorProvider(commandProcessor);

            Channel channel = new(Topic, new InMemoryMessageConsumer(_routingKey, _bus, _timeProvider, 1000));
            
            var messageMapperRegistry = new MessageMapperRegistry(
                null,
                new SimpleMessageMapperFactoryAsync(_ => new MyEventMessageMapperAsync()));
            messageMapperRegistry.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();
            
            _messagePump = new MessagePumpAsync<MyEvent>(provider, messageMapperRegistry, null, new InMemoryRequestContextFactory())
            {
                Channel = channel, TimeoutInMilliseconds = 5000, RequeueCount = 3, UnacceptableMessageLimit = 3
            };
            
            var unacceptableMessage1 = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), Topic, MessageType.MT_UNACCEPTABLE), 
                new MessageBody("")                                
            );
            var unacceptableMessage2 = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), Topic, MessageType.MT_UNACCEPTABLE), 
                new MessageBody("")
            );
            var unacceptableMessage3 = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), Topic, MessageType.MT_UNACCEPTABLE), 
                new MessageBody("")
            );
            var unacceptableMessage4 = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), Topic, MessageType.MT_UNACCEPTABLE), 
                new MessageBody("")
            );

            _bus.Enqueue(unacceptableMessage1);
            _bus.Enqueue(unacceptableMessage2);
            _bus.Enqueue(unacceptableMessage3);
            _bus.Enqueue(unacceptableMessage4);
        }

        [Fact]
        public async Task When_An_Unacceptable_Message_Limit_Is_Reached()
        {
            var task = Task.Factory.StartNew(() => _messagePump.Run(), TaskCreationOptions.LongRunning);
            
            _timeProvider.Advance(TimeSpan.FromSeconds(2)); //This will trigger requeue of not acked/rejected messages

            await Task.WhenAll(task);

            Assert.Empty(_bus.Stream(_routingKey));
            
            //TODO: How to undersetand that the channel shut down without inspection. Observability?
        }
    }
}

