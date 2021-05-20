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
using Paramore.Brighter.ServiceActivator.TestHelpers;

namespace Paramore.Brighter.Core.Tests.MessageDispatch
{
    public class MessagePumpEventProcessingExceptionTests
    {
        private readonly IAmAMessagePump _messagePump;
        private readonly FakeChannel _channel;
        private readonly SpyRequeueCommandProcessor _commandProcessor;
        private readonly int _requeueCount = 5;

        public MessagePumpEventProcessingExceptionTests()
        {
            _commandProcessor = new SpyRequeueCommandProcessor();
            _channel = new FakeChannel();
            var mapper = new MyEventMessageMapper();
            _messagePump = new MessagePumpBlocking<MyEvent>(_commandProcessor, mapper) { Channel = _channel, TimeoutInMilliseconds = 5000, RequeueCount = _requeueCount };

            var msg = mapper.MapToMessage(new MyEvent());
            _channel.Enqueue(msg);
        }

        [Fact]
        public void When_an_event_handler_throws_Then_message_is_requeued_until_rejected()
        {
            var task = Task.Factory.StartNew(() => _messagePump.Run(), TaskCreationOptions.LongRunning);
            Task.Delay(1000).Wait();

            var quitMessage = new Message(new MessageHeader(Guid.Empty, "", MessageType.MT_QUIT), new MessageBody(""));
            _channel.Enqueue(quitMessage);

            Task.WaitAll(new[] { task });

            _channel.RequeueCount.Should().Be(_requeueCount-1);
            _channel.RejectCount.Should().Be(1);
        }
    }
}
