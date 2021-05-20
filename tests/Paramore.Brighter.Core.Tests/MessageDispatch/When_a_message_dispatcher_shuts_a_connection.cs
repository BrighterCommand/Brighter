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
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Xunit;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.TestHelpers;

namespace Paramore.Brighter.Core.Tests.MessageDispatch
{
    public class MessageDispatcherShutConnectionTests : IDisposable
    {
        private static Dispatcher _dispatcher;
        private static Subscription _subscription;

        public MessageDispatcherShutConnectionTests()
        {
            var channel = new FakeChannel();
            IAmACommandProcessor commandProcessor = new SpyCommandProcessor();

            var messageMapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory((_) => new MyEventMessageMapper()));
            messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

            _subscription = new Subscription<MyEvent>(new SubscriptionName("test"), noOfPerformers: 3, timeoutInMilliseconds: 1000, channelFactory: new InMemoryChannelFactory(channel), channelName: new ChannelName("fakeChannel"), routingKey: new RoutingKey("fakekey"));
            _dispatcher = new Dispatcher(commandProcessor, messageMapperRegistry, new List<Subscription> { _subscription });

            var @event = new MyEvent();
            var message = new MyEventMessageMapper().MapToMessage(@event);
            for (var i = 0; i < 6; i++)
                channel.Enqueue(message);

            _dispatcher.State.Should().Be(DispatcherState.DS_AWAITING);
            _dispatcher.Receive();
        }

        [Fact]
        public void When_A_Message_Dispatcher_Shuts_A_Connection()
        {
            Task.Delay(1000).Wait();
            _dispatcher.Shut(_subscription);
            _dispatcher.End().Wait();

            //_should_have_consumed_the_messages_in_the_channel
            _dispatcher.Consumers.Should().NotContain(consumer => consumer.Name == _subscription.Name && consumer.State == ConsumerState.Open);
            //_should_have_a_stopped_state
            _dispatcher.State.Should().Be(DispatcherState.DS_STOPPED);
            //_should_have_no_consumers
            _dispatcher.Consumers.Should().BeEmpty();
        }
        
        public void Dispose()
        {
            if (_dispatcher?.State == DispatcherState.DS_RUNNING)
                _dispatcher.End().Wait();
        }
    }
}
