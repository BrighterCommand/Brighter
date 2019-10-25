#region Licence
/* The MIT License (MIT)
Copyright © 2015 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Reflection;
using FakeItEasy;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Xunit;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.Ports.Commands;
using Paramore.Brighter.ServiceActivator.Ports.Handlers;

namespace Paramore.Brighter.Core.Tests.ControlBus
{
    public class HeartbeatMessageTests
    {
        private const string TEST_FIRST_CONNECTION_NAME = "Test.First.Connection";
        private const string TEST_SECOND_CONNECTION_NAME = "Test.Second.Connection";
        private const string TEST_ROUTING_KEY = "test.routing.key";
        private readonly SpyCommandProcessor _spyCommandProcessor = new SpyCommandProcessor();
        private readonly Guid _correlationId = Guid.NewGuid();
        private readonly HeartbeatRequestCommandHandler _handler;
        private readonly HeartbeatRequest _heartbeatRequest;
        private readonly string _hostName;

        public HeartbeatMessageTests()
        {
            var dispatcher = A.Fake<IDispatcher>();
            var firstConsumer = A.Fake<IAmAConsumer>();
            var secondConsumer = A.Fake<IAmAConsumer>();

            A.CallTo(() => firstConsumer.Name).Returns(new ConsumerName(TEST_FIRST_CONNECTION_NAME));
            A.CallTo(() => firstConsumer.State).Returns(ConsumerState.Open);

            A.CallTo(() => secondConsumer.Name).Returns(new ConsumerName(TEST_SECOND_CONNECTION_NAME));
            A.CallTo(() => secondConsumer.State).Returns(ConsumerState.Shut);

            A.CallTo(() => dispatcher.Consumers).Returns(new List<IAmAConsumer> {firstConsumer, secondConsumer});

            var hostName = new HostName($"{Environment.MachineName}.{Assembly.GetEntryAssembly()?.FullName}");
            A.CallTo(() => dispatcher.HostName).Returns(hostName);
            _hostName = hostName;

            _heartbeatRequest = new HeartbeatRequest(new ReplyAddress(TEST_ROUTING_KEY, _correlationId));
            _handler = new HeartbeatRequestCommandHandler(_spyCommandProcessor, dispatcher);
        }

        [Fact]
        public void When_recieving_a_heartbeat_message()
        {
            _handler.Handle(_heartbeatRequest);

            //_should_post_back_a_heartbeat_response
            _spyCommandProcessor.ContainsCommand(CommandType.Post);
            //_should_have_diagnostic_information_in_the_response
            var heartbeatEvent = _spyCommandProcessor.Observe<HeartbeatReply>();
            heartbeatEvent.HostName.Should().Be(_hostName);
            heartbeatEvent.SendersAddress.Topic.Should().Be(TEST_ROUTING_KEY);
            heartbeatEvent.SendersAddress.CorrelationId.Should().Be(_correlationId);
            heartbeatEvent.Consumers[0].ConsumerName.ToString().Should().Be(TEST_FIRST_CONNECTION_NAME);
            heartbeatEvent.Consumers[0].State.Should().Be(ConsumerState.Open);
            heartbeatEvent.Consumers[1].ConsumerName.ToString().Should().Be(TEST_SECOND_CONNECTION_NAME);
            heartbeatEvent.Consumers[1].State.Should().Be(ConsumerState.Shut);
        }
   }
}
