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
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Xunit;
using Paramore.Brighter.ServiceActivator;

namespace Paramore.Brighter.Core.Tests.MessageDispatch
{

    public class MessageDispatcherMultiplePerformerTests
    {
        private const string Topic = "myTopic";
        private readonly Dispatcher _dispatcher;
        private readonly InternalBus _bus;

        public MessageDispatcherMultiplePerformerTests()
        {
            var routingKey = new RoutingKey(Topic);
            _bus = new InternalBus();
            var consumer = new InMemoryMessageConsumer(routingKey, _bus, TimeProvider.System, 1000);
            
            IAmAChannel channel = new Channel(new (Topic), consumer, 6);
            IAmACommandProcessor commandProcessor = new SpyCommandProcessor();

            var messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory((_) => new MyEventMessageMapper()),
                null);
            messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

            var connection = new Subscription<MyEvent>(
                new SubscriptionName("test"), 
                noOfPerformers: 3, 
                timeoutInMilliseconds: 100, 
                channelFactory: new InMemoryChannelFactory(_bus, TimeProvider.System), 
                channelName: new ChannelName("fakeChannel"), 
                routingKey: routingKey
            );
            _dispatcher = new Dispatcher(commandProcessor, new List<Subscription> { connection }, messageMapperRegistry);

            var @event = new MyEvent();
            var message = new MyEventMessageMapper().MapToMessage(@event, new Publication{Topic = connection.RoutingKey});
            for (var i = 0; i < 6; i++)
                channel.Enqueue(message);

            _dispatcher.State.Should().Be(DispatcherState.DS_AWAITING);
            _dispatcher.Receive();
        }

#pragma warning disable xUnit1031
        [Fact]
        public void WhenAMessageDispatcherStartsMultiplePerformers()
        {
            _dispatcher.State.Should().Be(DispatcherState.DS_RUNNING);
            _dispatcher.Consumers.Count().Should().Be(3);

            _dispatcher.End().Wait();
            
            _bus.Stream(new RoutingKey(Topic)).Count().Should().Be(0); 
            _dispatcher.State.Should().Be(DispatcherState.DS_STOPPED);
        }
#pragma warning restore xUnit1031
    }
}
