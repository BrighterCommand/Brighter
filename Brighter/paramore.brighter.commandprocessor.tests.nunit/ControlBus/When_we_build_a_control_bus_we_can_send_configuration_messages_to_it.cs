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
using FakeItEasy;
using nUnitShouldAdapter;
using NUnit.Specifications;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.serviceactivator;
using paramore.brighter.serviceactivator.controlbus;
using paramore.brighter.serviceactivator.Ports.Commands;
using paramore.brighter.serviceactivator.TestHelpers;

namespace paramore.brighter.commandprocessor.tests.nunit.ControlBus
{
    public class When_we_build_a_control_bus_we_can_send_configuration_messages_to_it : NUnit.Specifications.ContextSpecification
    {
        private static IDispatcher s_dispatcher;
        private static Dispatcher s_controlBus;
        private static ControlBusReceiverBuilder s_busReceiverBuilder;
        private static ConfigurationCommand s_configurationCommand;
        private static Exception s_exception;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();
            s_dispatcher = A.Fake<IDispatcher>();
            var messageProducerFactory = A.Fake<IAmAMessageProducerFactory>();

            s_busReceiverBuilder = (ControlBusReceiverBuilder) ControlBusReceiverBuilder
                .With()
                .Dispatcher(s_dispatcher)
                .ProducerFactory(messageProducerFactory)
                .ChannelFactory(new InMemoryChannelFactory());

            s_controlBus = s_busReceiverBuilder.Build("tests");

            s_configurationCommand = new ConfigurationCommand(ConfigurationCommandType.CM_STARTALL);

        };

        private Because _of = () => s_exception = Catch.Exception(() => s_controlBus.CommandProcessor.Send(s_configurationCommand));

        private It should_not_raise_exceptions_for_missing_handlers = () => s_exception.ShouldBeNull();
        private It should_call_the_dispatcher_to_start_it = () => A.CallTo(() => s_dispatcher.Receive()).MustHaveHappened();

    }
}
