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
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Proactor
{
    public class AsyncMessagePumpUnacceptableMessageTests
    {
        private const string Channel = "MyChannel";
        private readonly IAmAMessagePump _messagePump;
        private readonly ChannelAsync _channel;
        private readonly InternalBus _bus;
        private readonly RoutingKey _routingKey = new("MyTopic");
        private readonly FakeTimeProvider _timeProvider = new FakeTimeProvider();

        public AsyncMessagePumpUnacceptableMessageTests()
        {
            SpyRequeueCommandProcessor commandProcessor = new();
            
            _bus = new InternalBus();
            
            _channel = new ChannelAsync(
                new(Channel), _routingKey, 
                new InMemoryMessageConsumer(_routingKey, _bus, _timeProvider, TimeSpan.FromMilliseconds(1000))
            );
            
            var messageMapperRegistry = new MessageMapperRegistry(
                null,
                new SimpleMessageMapperFactoryAsync(_ => new MyEventMessageMapperAsync()));
            messageMapperRegistry.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();
            
            _messagePump = new ServiceActivator.Proactor(commandProcessor, (message) => typeof(MyEvent), messageMapperRegistry, null, new InMemoryRequestContextFactory(), _channel)
            {
                Channel = _channel, TimeOut = TimeSpan.FromMilliseconds(5000), RequeueCount = 3
            };

            var myMessage = JsonSerializer.Serialize(new MyEvent());
            var unacceptableMessage = new Message(new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_UNACCEPTABLE), new MessageBody(myMessage));

            _bus.Enqueue(unacceptableMessage);
            
        }

        [Fact]
        public async Task When_An_Unacceptable_Message_Is_Recieved()
        {
            var task = Task.Factory.StartNew(() => _messagePump.Run(), TaskCreationOptions.LongRunning);
            await Task.Delay(1000);
            
            _timeProvider.Advance(TimeSpan.FromSeconds(2)); //This will trigger requeue of not acked/rejected messages

            var quitMessage = new Message(
                new MessageHeader(string.Empty, RoutingKey.Empty, MessageType.MT_QUIT), 
                new MessageBody("")
            );
            _channel.Enqueue(quitMessage);

            await Task.WhenAll(new[] { task });

            //should_acknowledge_the_message
            Assert.Empty(_bus.Stream(_routingKey));
        }
    }
}
