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
using System.Linq;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Xunit;
using Paramore.Brighter.ServiceActivator;
using System.Text.Json;
using FakeItEasy;

namespace Paramore.Brighter.Core.Tests.MessageDispatch
{
    public class MessagePumpRetryEventConnectionFailureTests
    {
        private readonly IAmAMessagePump _messagePump;
        private readonly SpyCommandProcessor _commandProcessor;

        public MessagePumpRetryEventConnectionFailureTests()
        {
            _commandProcessor = new SpyCommandProcessor();
            var provider = new CommandProcessorProvider(_commandProcessor);
            var channel = new FailingChannel { NumberOfRetries = 1 };
            var messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory(_ => new MyEventMessageMapper()),
                null);
            messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();
            
            _messagePump = new MessagePumpBlocking<MyEvent>(provider, messageMapperRegistry, null)
            {
                Channel = channel, TimeoutInMilliseconds = 500, RequeueCount = -1
            };

            var @event = new MyEvent();

            //Two events will be received when channel fixed
            var message1 = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), "MyTopic", MessageType.MT_EVENT), 
                new MessageBody(JsonSerializer.Serialize(@event, JsonSerialisationOptions.Options))
            );
            var message2 = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), "MyTopic", MessageType.MT_EVENT), 
                new MessageBody(JsonSerializer.Serialize(@event, JsonSerialisationOptions.Options))
            );
            channel.Enqueue(message1);
            channel.Enqueue(message2);
            
            //Quit the message pump
            var quitMessage = new Message(new MessageHeader(string.Empty, "", MessageType.MT_QUIT), new MessageBody(""));
            channel.Enqueue(quitMessage);
        }

        [Fact]
        public void When_A_Channel_Failure_Exception_Is_Thrown_For_Event_Should_Retry_Until_Connection_Re_established()
        {
            _messagePump.Run();

            //_should_publish_the_message_via_the_command_processor
            _commandProcessor.Commands.Count().Should().Be(2);
            _commandProcessor.Commands[0].Should().Be(CommandType.Publish);
            _commandProcessor.Commands[1].Should().Be(CommandType.Publish);
        }

    }
}
