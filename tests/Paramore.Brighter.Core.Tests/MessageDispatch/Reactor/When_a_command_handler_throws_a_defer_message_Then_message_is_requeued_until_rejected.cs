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
using Paramore.Brighter.ServiceActivator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Reactor
{
    public class MessagePumpCommandProcessingDeferMessageActionTests
    {
        private readonly IAmAMessagePump _messagePump;
        private readonly Channel _channel;
        private readonly int _requeueCount = 5;
        private readonly InternalBus _bus = new();
        private readonly RoutingKey _routingKey = new("MyCommand");
        private readonly FakeTimeProvider _timeProvider = new();

        public MessagePumpCommandProcessingDeferMessageActionTests()
        {
            SpyRequeueCommandProcessor commandProcessor = new();

            _channel = new Channel(new("myChannel"), _routingKey, new InMemoryMessageConsumer(_routingKey, _bus, _timeProvider, TimeSpan.FromMilliseconds(1000)));
            
            var messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory(_ => new MyCommandMessageMapper()),
                null);
            messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();
            
            _messagePump = new ServiceActivator.Reactor(commandProcessor, (message) => typeof(MyCommand), 
                messageMapperRegistry, null, new InMemoryRequestContextFactory(), _channel)
            {
                Channel = _channel, TimeOut = TimeSpan.FromMilliseconds(5000), RequeueCount = _requeueCount
            };

            var msg = new TransformPipelineBuilder(messageMapperRegistry, null)
                .BuildWrapPipeline<MyCommand>()
                .Wrap(new MyCommand(), new RequestContext(), new Publication{Topic = _routingKey});

            _bus.Enqueue(msg);
            
        }

        [Fact]
        public async Task When_a_command_handler_throws_a_defer_message_Then_message_is_requeued_until_rejected()
        {
            var task = Task.Factory.StartNew(() => _messagePump.Run(), TaskCreationOptions.LongRunning);
            await Task.Delay(1000);
            
            _timeProvider.Advance(TimeSpan.FromSeconds(2)); //This will trigger requeue of not acked/rejected messages

            var quitMessage = MessageFactory.CreateQuitMessage(_routingKey);
            _channel.Enqueue(quitMessage);

            await Task.WhenAll(task);
            
            Assert.Empty(_bus.Stream(_routingKey));

        }
    }
}
