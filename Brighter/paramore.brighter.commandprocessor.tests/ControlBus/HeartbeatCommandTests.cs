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
using FakeItEasy;
using Machine.Specifications;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.serviceactivator;
using paramore.brighter.serviceactivator.Ports.Commands;
using paramore.brighter.serviceactivator.Ports.Handlers;
using paramore.commandprocessor.tests.MessageDispatch.TestDoubles;

namespace paramore.commandprocessor.tests.ControlBus
{
    public class When_recieving_a_heartbeat_message
    {
        private const string TEST_FIRST_CONNECTION_NAME = "Test.First.Connection";
        private const string TEST_SECOND_CONNECTION_NAME = "Test.Second.Connection";
        private const string TEST_ROUTING_KEY = "test.routing.key";
        private static readonly SpyCommandProcessor s_spyCommandProcessor = new SpyCommandProcessor();
        private static readonly Guid s_CorrelationId = Guid.NewGuid();
        private static HeartbeatCommandHandler s_handler;
        private static HeartbeatCommand s_heartbeatCommand;
        private static string s_hostName;
        private static IAmAConsumer s_firstConsumer;
        private static IAmAConsumer s_secondConsumer;

        private Establish context = () =>
        {
            var logger = A.Fake<ILog>();
            var dispatcher = A.Fake<IDispatcher>();

            s_firstConsumer = A.Fake<IAmAConsumer>();
            A.CallTo(() => s_firstConsumer.Name).Returns(new ConnectionName(TEST_FIRST_CONNECTION_NAME));
            A.CallTo(() => s_firstConsumer.State).Returns(ConsumerState.Open);

            s_secondConsumer = A.Fake<IAmAConsumer>();
            A.CallTo(() => s_secondConsumer.Name).Returns(new ConnectionName(TEST_SECOND_CONNECTION_NAME));
            A.CallTo(() => s_secondConsumer.State).Returns(ConsumerState.Shut);

            A.CallTo(() => dispatcher.Consumers).Returns(new List<IAmAConsumer>() {s_firstConsumer, s_secondConsumer});

            var hostName = new HostName(Environment.MachineName + "." +System.Reflection.Assembly.GetExecutingAssembly().GetName().Name);
            A.CallTo(() => dispatcher.HostName).Returns(hostName);
            s_hostName = hostName;

            s_handler = new HeartbeatCommandHandler(logger, s_spyCommandProcessor, dispatcher)
            {
                Context = new RequestContext() {Callback = new Callback(TEST_ROUTING_KEY, s_CorrelationId)}
            };

        };

        private Because of = () => s_handler.Handle(s_heartbeatCommand);

        private It _should_post_back_a_heartbeat_response = () => s_spyCommandProcessor.Commands.ShouldContain(ct => ct == CommandType.Post);

        private It _should_have_diagnostic_information_in_the_response = () =>
        {
            var heartbeatEvent = s_spyCommandProcessor.Observe<HeartbeatEvent>();
            heartbeatEvent.ShouldMatch(
                hb => hb.HostName == s_hostName
                && hb.Consumers[0].ConnectionName == TEST_FIRST_CONNECTION_NAME 
                && hb.Consumers[0].State == ConsumerState.Open
                && hb.Consumers[1].ConnectionName == TEST_SECOND_CONNECTION_NAME
                && hb.Consumers[1].State == ConsumerState.Shut);
        };

        private It _should_post_back_to_a_reply_to_queue_from_the_originating_message = () => s_spyCommandProcessor.Topic.ShouldEqual(TEST_ROUTING_KEY);
    }
}
