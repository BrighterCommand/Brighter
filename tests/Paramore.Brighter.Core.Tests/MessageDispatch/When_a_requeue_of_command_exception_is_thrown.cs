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
using System.Linq;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Xunit;
using Paramore.Brighter.ServiceActivator;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;

namespace Paramore.Brighter.Core.Tests.MessageDispatch
{
    public class MessagePumpCommandRequeueTests
    {
        private const string Topic = "MyTopic";
        private readonly RoutingKey _routingKey = new(Topic);
        private readonly InternalBus _bus = new();
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly IAmAMessagePump _messagePump;
        private readonly SpyCommandProcessor _commandProcessor;
        private readonly MyCommand _command = new();

        public MessagePumpCommandRequeueTests()
        {
            _commandProcessor = new SpyRequeueCommandProcessor();
            var provider = new CommandProcessorProvider(_commandProcessor);
            Channel channel = new(Topic, new InMemoryMessageConsumer(_routingKey, _bus, _timeProvider, 1000), 2);
            var messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory(_ => new MyCommandMessageMapper()),
                null);
             messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();
             _messagePump = new MessagePumpBlocking<MyCommand>(provider, messageMapperRegistry, null, new InMemoryRequestContextFactory()) 
                { Channel = channel, TimeoutInMilliseconds = 5000, RequeueCount = -1 };

            var message1 = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), Topic, MessageType.MT_COMMAND), 
                new MessageBody(JsonSerializer.Serialize(_command, JsonSerialisationOptions.Options))
            );
            
            var message2 = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), Topic, MessageType.MT_COMMAND), 
                new MessageBody(JsonSerializer.Serialize(_command, JsonSerialisationOptions.Options))
            );
            
            channel.Enqueue(message1);
            channel.Enqueue(message2);
            var quitMessage = new Message(
                new MessageHeader(string.Empty, "", MessageType.MT_QUIT), 
                new MessageBody("")
            );
            channel.Enqueue(quitMessage);
        }

        [Fact]
        public void When_A_Requeue_Of_Command_Exception_Is_Thrown()
        {
            _messagePump.Run();

            _commandProcessor.Commands[0].Should().Be(CommandType.Send);
            
            Assert.Equal(2, _bus.Stream(_routingKey).Count());
            
            
        }
    }
}
