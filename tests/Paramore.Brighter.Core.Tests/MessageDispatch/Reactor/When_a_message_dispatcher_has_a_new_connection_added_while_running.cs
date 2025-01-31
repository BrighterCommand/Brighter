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

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Reactor
{
    [Collection("CommandProcessor")]
    public class DispatcherAddNewConnectionTests : IDisposable
    {
        private readonly Dispatcher _dispatcher;
        private readonly Subscription _newSubscription;
        private readonly InternalBus _bus;
        private readonly RoutingKey _routingKey = new("MyEvent");
        private readonly RoutingKey _routingKeyTwo = new("OtherEvent");

        public DispatcherAddNewConnectionTests()
        {
            _bus = new InternalBus();
            
            IAmACommandProcessor commandProcessor = new SpyCommandProcessor();

            var messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory((_) => new MyEventMessageMapper()),
                null);
            messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

            Subscription subscription = new Subscription<MyEvent>(
                new SubscriptionName("test"), noOfPerformers: 1, timeOut: TimeSpan.FromMilliseconds(1000), 
                channelFactory: new InMemoryChannelFactory(_bus, TimeProvider.System), channelName: new ChannelName("fakeChannel"), 
                messagePumpType: MessagePumpType.Reactor, routingKey: _routingKey
            );
            
            _newSubscription = new Subscription<MyEvent>(
                new SubscriptionName("newTest"), noOfPerformers: 1, timeOut: TimeSpan.FromMilliseconds(1000), 
                channelFactory: new InMemoryChannelFactory(_bus, TimeProvider.System), 
                channelName: new ChannelName("fakeChannelTwo"), messagePumpType: MessagePumpType.Reactor, routingKey: _routingKeyTwo);
            _dispatcher = new Dispatcher(commandProcessor, new List<Subscription> { subscription }, messageMapperRegistry);

            var @event = new MyEvent();
            var message = new MyEventMessageMapper().MapToMessage(@event, new Publication{Topic = _routingKey});
            _bus.Enqueue(message);

            _dispatcher.State.Should().Be(DispatcherState.DS_AWAITING);
            _dispatcher.Receive();
        }

        [Fact]
        public async Task When_A_Message_Dispatcher_Has_A_New_Connection_Added_While_Running()
        {
            _dispatcher.Open(_newSubscription);

            var @event = new MyEvent();
            var message = new MyEventMessageMapper().MapToMessage(@event, new Publication{Topic = _routingKeyTwo});
            _bus.Enqueue(message);

            await Task.Delay(1000);

            _bus.Stream(_routingKey).Count().Should().Be(0);
            _dispatcher.State.Should().Be(DispatcherState.DS_RUNNING);
            _dispatcher.Consumers.Should().HaveCount(2);
            _dispatcher.Subscriptions.Should().HaveCount(2);
        }

        public void Dispose()
        {
            if (_dispatcher?.State == DispatcherState.DS_RUNNING)
                _dispatcher.End().Wait();
        }
    }
}
