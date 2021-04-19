﻿using System;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Xunit;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.TestHelpers;

namespace Paramore.Brighter.Core.Tests.MessageDispatch
{

    public class MessagePumpFailingMessageTranslationTests
    {
        private readonly IAmAMessagePump _messagePump;
        private readonly FakeChannel _channel;
        private readonly SpyRequeueCommandProcessor _commandProcessor;

        public MessagePumpFailingMessageTranslationTests()
        {
            _commandProcessor = new SpyRequeueCommandProcessor();
            _channel = new FakeChannel();
            var mapper = new FailingEventMessageMapper();
            _messagePump = new MessagePumpBlocking<MyFailingMapperEvent>(_commandProcessor, mapper) { Channel = _channel, TimeoutInMilliseconds = 5000, RequeueCount = 3, UnacceptableMessageLimit = 3 };

            var unmappableMessage = new Message(new MessageHeader(Guid.NewGuid(), "MyTopic", MessageType.MT_EVENT), new MessageBody("{ \"Id\" : \"48213ADB-A085-4AFF-A42C-CF8209350CF7\" }"));

            _channel.Enqueue(unmappableMessage);
        }

        [Fact]
        public void When_A_Message_Fails_To_Be_Mapped_To_A_Request ()
        {
            var task = Task.Factory.StartNew(() => _messagePump.Run(), TaskCreationOptions.LongRunning);

            _channel.Stop();

            Task.WaitAll(new[] { task });

            //should_have_acknowledge_the_message
            _channel.AcknowledgeHappened.Should().BeTrue();
        }
    }
}
