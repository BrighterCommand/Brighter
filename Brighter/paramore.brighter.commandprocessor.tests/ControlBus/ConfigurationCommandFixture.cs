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

using FakeItEasy;
using Machine.Specifications;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.serviceactivator;
using paramore.brighter.serviceactivator.controlbus.Ports.Commands;
using paramore.brighter.serviceactivator.controlbus.Ports.Handlers;

namespace paramore.commandprocessor.tests.ControlBus
{
    public class When_receiving_an_all_stop_message
    {
        private static ConfigurationMessageHandler s_configurationMessageHandler;
        private static ConfigurationCommand s_configurationCommand;
        private static IDispatcher s_dispatcher;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();
            s_dispatcher = A.Fake<IDispatcher>();

            s_configurationMessageHandler = new ConfigurationMessageHandler(logger, s_dispatcher);

            s_configurationCommand = new ConfigurationCommand(ConfigurationCommandType.CM_STOPALL);
        };

        private Because _of = () => s_configurationMessageHandler.Handle(s_configurationCommand);

        private It _should_call_end_on_the_dispatcher = () => A.CallTo(() => s_dispatcher.End()).MustHaveHappened();
    }

    public class When_receiving_an_all_start_message
    {
        private static ConfigurationMessageHandler s_configurationMessageHandler;
        private static ConfigurationCommand s_configurationCommand;
        private static IDispatcher s_dispatcher;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();
            s_dispatcher = A.Fake<IDispatcher>();

            s_configurationMessageHandler = new ConfigurationMessageHandler(logger, s_dispatcher);

            s_configurationCommand = new ConfigurationCommand(ConfigurationCommandType.CM_STARTALL);
        };

        private Because _of = () => s_configurationMessageHandler.Handle(s_configurationCommand);

        private It _should_call_receive_on_the_dispatcher = () => A.CallTo(() => s_dispatcher.Receive()).MustHaveHappened();
    }

    public class When_receiving_a_stop_message_for_a_connection
    {
        const string CONNECTION_NAME = "Test";
        private static ConfigurationMessageHandler s_configurationMessageHandler;
        private static ConfigurationCommand s_configurationCommand;
        private static IDispatcher s_dispatcher;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();
            s_dispatcher = A.Fake<IDispatcher>();

            s_configurationMessageHandler = new ConfigurationMessageHandler(logger, s_dispatcher);

            s_configurationCommand = new ConfigurationCommand(ConfigurationCommandType.CM_STOPCHANNEL) {ConnectionName = CONNECTION_NAME};
        };

        private Because _of = () => s_configurationMessageHandler.Handle(s_configurationCommand);

        private It _should_call_stop_for_the_given_connection = () => A.CallTo(() => s_dispatcher.Shut(CONNECTION_NAME));
    }

    public class When_receiving_a_start_message_for_a_connection
    {
        const string CONNECTION_NAME = "Test";
        private static ConfigurationMessageHandler s_configurationMessageHandler;
        private static ConfigurationCommand s_configurationCommand;
        private static IDispatcher s_dispatcher;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();
            s_dispatcher = A.Fake<IDispatcher>();

            s_configurationMessageHandler = new ConfigurationMessageHandler(logger, s_dispatcher);

            s_configurationCommand = new ConfigurationCommand(ConfigurationCommandType.CM_STARTCHANNEL) {ConnectionName = CONNECTION_NAME};
        };

        private Because _of = () => s_configurationMessageHandler.Handle(s_configurationCommand);

        private It _should_call_stop_for_the_given_connection = () => A.CallTo(() => s_dispatcher.Shut(CONNECTION_NAME));
    }
}
