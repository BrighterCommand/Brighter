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
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.TestHelpers;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Tests.MessageDispatch.TestDoubles;

namespace Paramore.Brighter.Tests.MessageDispatch
{
    public class PerformerCanStopTests
    {
        private readonly Performer _performer;
        private readonly SpyCommandProcessor _commandProcessor;
        private readonly FakeChannel _channel;
        private readonly Task _performerTask;

        public PerformerCanStopTests()
        {
            _commandProcessor = new SpyCommandProcessor();
            _channel = new FakeChannel();
            var mapper = new MyEventMessageMapper();
            var messagePump = new MessagePump<MyEvent>(_commandProcessor, mapper);
            messagePump.Channel = _channel;
            messagePump.TimeoutInMilliseconds = 5000;

            var @event = new MyEvent();
            var message = new Message(new MessageHeader(Guid.NewGuid(), "MyTopic", MessageType.MT_EVENT), new MessageBody(JsonConvert.SerializeObject(@event)));
            _channel.Add(message);

            _performer = new Performer(_channel, messagePump);
            _performerTask = _performer.Run();
            _performer.Stop();
        }

        [Fact]
        public void When_Running_A_Message_Pump_On_A_Thread_Should_Be_Able_To_Stop()
        {
            _performerTask.Wait();

            //_should_terminate_successfully
            _performerTask.IsCompleted.Should().BeTrue();
            //_should_not_have_errored
            _performerTask.IsFaulted.Should().BeFalse();
            //_should_not_show_as_cancelled
            _performerTask.IsCanceled.Should().BeFalse();
            //_should_have_consumed_the_messages_in_the_channel
            _channel.Length.Should().Be(0);
        }
    }
}
