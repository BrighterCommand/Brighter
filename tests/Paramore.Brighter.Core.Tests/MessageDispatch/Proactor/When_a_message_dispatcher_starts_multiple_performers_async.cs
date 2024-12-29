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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
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

            _dispatcher.State.Should().Be(DispatcherState.DS_AWAITING);
            _dispatcher.Receive();
        }

        [Fact]
        public async Task WhenAMessageDispatcherStartsMultiplePerformers()
        {
            _dispatcher.State.Should().Be(DispatcherState.DS_RUNNING);
            _dispatcher.Consumers.Count().Should().Be(3);

            await _dispatcher.End();
            
            _bus.Stream(new RoutingKey(Topic)).Count().Should().Be(0); 
            _dispatcher.State.Should().Be(DispatcherState.DS_STOPPED);
        }
    }
}
