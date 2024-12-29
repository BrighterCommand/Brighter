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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Reactor
{
    [Collection("CommandProcessor")]
    public class MessageDispatcherMultipleConnectionTests : IDisposable
    {
        private readonly Dispatcher _dispatcher;
        private int _numberOfConsumers;
        private readonly InternalBus _bus = new();
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly RoutingKey _commandRoutingKey = new("myCommand");
        private readonly RoutingKey _eventRoutingKey = new("myEvent");

        public MessageDispatcherMultipleConnectionTests()
        {
            var commandProcessor = new SpyCommandProcessor();

            var container = new ServiceCollection();
            container.AddTransient<MyEventMessageMapper>();
            container.AddTransient<MyCommandMessageMapper>();

            var messageMapperRegistry = new MessageMapperRegistry(
                new ServiceProviderMapperFactory(container.BuildServiceProvider()),
                null);
            messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();
            messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();

            var myEventConnection = new Subscription<MyEvent>(
                new SubscriptionName("test"), noOfPerformers: 1, timeOut: TimeSpan.FromMilliseconds(1000), channelFactory: 
                new InMemoryChannelFactory(_bus, _timeProvider), messagePumpType: MessagePumpType.Reactor, channelName: new ChannelName("fakeEventChannel"), 
                routingKey: _eventRoutingKey
            );
            var myCommandConnection = new Subscription<MyCommand>(
                new SubscriptionName("anothertest"), noOfPerformers: 1, timeOut: TimeSpan.FromMilliseconds(1000), 
                channelFactory: new InMemoryChannelFactory(_bus, _timeProvider), 
                channelName: new ChannelName("fakeCommandChannel"), messagePumpType: MessagePumpType.Reactor, routingKey: _commandRoutingKey
                );
            _dispatcher = new Dispatcher(commandProcessor, new List<Subscription> { myEventConnection, myCommandConnection }, messageMapperRegistry);

            var @event = new MyEvent();
            var eventMessage = new MyEventMessageMapper().MapToMessage(@event, new Publication{Topic = _eventRoutingKey});
            _bus.Enqueue(eventMessage);

            var command = new MyCommand();
            var commandMessage = new MyCommandMessageMapper().MapToMessage(command, new Publication{Topic = _commandRoutingKey});
            _bus.Enqueue(commandMessage);

            _dispatcher.State.Should().Be(DispatcherState.DS_AWAITING);
            _dispatcher.Receive();
        }


#pragma warning disable xUnit1031
        [Fact]
        public void When_A_Message_Dispatcher_Starts_Different_Types_Of_Performers()
        {
            Task.Delay(1000).Wait();
            _numberOfConsumers = _dispatcher.Consumers.Count();
            
            _timeProvider.Advance(TimeSpan.FromSeconds(2)); //This will trigger requeue of not acked/rejected messages
            
            _dispatcher.End().Wait();

            Assert.Empty(_bus.Stream(_eventRoutingKey));
            Assert.Empty(_bus.Stream(_commandRoutingKey));
            _dispatcher.State.Should().Be(DispatcherState.DS_STOPPED);
            _dispatcher.Consumers.Should().BeEmpty();
            _numberOfConsumers.Should().Be(2);
        }
#pragma warning restore xUnit1031
        
        public void Dispose()
        {
            if (_dispatcher?.State == DispatcherState.DS_RUNNING)
                _dispatcher.End().Wait();
        }

    }

}
