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
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.TestHelpers;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Tests.MessageDispatch.TestDoubles;
using Polly.Registry;

namespace Paramore.Brighter.Tests.MessageDispatch
{
    public class MessagePumpDispatchTests
    {
        private readonly IAmAMessagePump _messagePump;
        private readonly MyEvent _myEvent = new MyEvent();
        private readonly IDictionary<string, Guid> _receivedMessages = new Dictionary<string, Guid>();

        public MessagePumpDispatchTests()
        {
            var subscriberRegistry = new SubscriberRegistry();
            subscriberRegistry.Register<MyEvent, MyEventHandler>();

            var handlerFactory = new TestHandlerFactory<MyEvent, MyEventHandler>(() => new MyEventHandler(_receivedMessages));

            var commandProcessor = new CommandProcessor(
                subscriberRegistry,
                handlerFactory, 
                new InMemoryRequestContextFactory(), 
                new PolicyRegistry());
            
            PipelineBuilder<MyEvent>.ClearPipelineCache();

            var channel = new FakeChannel();
            var mapper = new MyEventMessageMapper();
            _messagePump = new MessagePump<MyEvent>(commandProcessor, mapper) { Channel = channel, TimeoutInMilliseconds = 5000 };

            var message = new Message(new MessageHeader(Guid.NewGuid(), "MyTopic", MessageType.MT_EVENT), new MessageBody(JsonConvert.SerializeObject(_myEvent)));
            channel.Enqueue(message);
            var quitMessage = new Message(new MessageHeader(Guid.Empty, "", MessageType.MT_QUIT), new MessageBody(""));
            channel.Enqueue(quitMessage);
        }

        [Fact]
        public void When_A_Message_Is_Dispatched_It_Should_Reach_A_Handler()
        {
            _messagePump.Run();

            //_should_dispatch_the_message_to_a_handler
            _receivedMessages.Should().Contain(nameof(MyEventHandler), _myEvent.Id);
        }
    }
}
