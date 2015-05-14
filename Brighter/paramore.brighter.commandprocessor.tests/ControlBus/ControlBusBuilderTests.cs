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
using FakeItEasy;
using FluentAssertions;
using Machine.Specifications;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.serviceactivator;
using paramore.brighter.serviceactivator.controlbus;
using paramore.brighter.serviceactivator.Ports;
using paramore.brighter.serviceactivator.Ports.Commands;
using paramore.brighter.serviceactivator.TestHelpers;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;
using Polly;
using TinyIoC;

namespace paramore.commandprocessor.tests.ControlBus
{
    public class When_configuring_a_control_bus
    {
        private static Dispatcher s_controlBus;
        private static ControlBusBuilder s_busBuilder;
        private static IDispatcher s_dispatcher;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();
            s_dispatcher = A.Fake<IDispatcher>();

            s_busBuilder = ControlBusBuilder
                .With()
                .Logger(logger)
                .Dispatcher(s_dispatcher)
                .ChannelFactory(new InMemoryChannelFactory()) as ControlBusBuilder;
        };

        private Because _of = () => s_controlBus = s_busBuilder.Build("tests");

        private It _should_have_a_configuration_channel = () => s_controlBus.Connections.Any(cn => cn.Name == ControlBusBuilder.CONFIGURATION).ShouldBeTrue();
        private It _should_have_a_heartbeat_channel = () => s_controlBus.Connections.Any(cn => cn.Name == ControlBusBuilder.HEARTBEAT).ShouldBeTrue();
        private It _should_have_a_command_processor = () => s_controlBus.CommandProcessor.ShouldNotBeNull();
    }

    public class When_we_build_a_control_bus_we_can_send_configuration_messages_to_it
    {
        private static IDispatcher s_dispatcher;
        private static Dispatcher s_controlBus;
        private static ControlBusBuilder s_busBuilder;
        private static ConfigurationCommand s_configurationCommand;
        private static Exception s_exception;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();
            s_dispatcher = A.Fake<IDispatcher>();

            s_busBuilder = (ControlBusBuilder) ControlBusBuilder
                .With()
                .Logger(logger)
                .Dispatcher(s_dispatcher)
                .ChannelFactory(new InMemoryChannelFactory());

            s_controlBus = s_busBuilder.Build("tests");

            s_configurationCommand = new ConfigurationCommand(ConfigurationCommandType.CM_STARTALL);

        };

        private Because _of = () => s_exception = Catch.Exception(() => s_controlBus.CommandProcessor.Send(s_configurationCommand));

        private It should_not_raise_exceptions_for_missing_handlers = () => s_exception.ShouldBeNull();
        private It should_call_the_dispatcher_to_start_it = () => A.CallTo(() => s_dispatcher.Receive()).MustHaveHappened();

    }
}
