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
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.serviceactivator;
using paramore.brighter.serviceactivator.Ports.Commands;
using paramore.brighter.serviceactivator.Ports.Handlers;

namespace paramore.brighter.commandprocessor.tests.nunit.ControlBus
{
    public class When_receiving_an_all_stop_message : NUnit.Specifications.ContextSpecification
    {
        private static ConfigurationCommandHandler s_configurationCommandHandler;
        private static ConfigurationCommand s_configurationCommand;
        private static IDispatcher s_dispatcher;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();
            s_dispatcher = A.Fake<IDispatcher>();

            s_configurationCommandHandler = new ConfigurationCommandHandler(s_dispatcher, logger);

            s_configurationCommand = new ConfigurationCommand(ConfigurationCommandType.CM_STOPALL);
        };

        private Because _of = () => s_configurationCommandHandler.Handle(s_configurationCommand);

        private It _should_call_end_on_the_dispatcher = () => A.CallTo(() => s_dispatcher.End()).MustHaveHappened();
    }
}