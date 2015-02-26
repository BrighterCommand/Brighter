// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
}
