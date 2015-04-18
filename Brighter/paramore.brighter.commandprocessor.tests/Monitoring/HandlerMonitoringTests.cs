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
using System.Configuration;
using FakeItEasy;
using Machine.Specifications;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.monitoring.Events;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;
using paramore.commandprocessor.tests.MessageDispatch.TestDoubles;

namespace paramore.commandprocessor.tests.Monitoring
{
    public class WhenMonitoringIsOnForAHandler
    {
        static MyMonitoredHandler s_handler;
        static MyCommand s_command;
        static SpyCommandProcessor s_commandProcessor;
        static DateTime s_at;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();

            s_commandProcessor = new SpyCommandProcessor();
            s_handler = new MyMonitoredHandler(logger, s_commandProcessor);

            ConfigurationManager.AppSettings["IsMonitoringEnabled"] = "true";
            s_at = DateTime.UtcNow;
            Clock.OverrideTime = s_at;
        };

        private Because _of = () => s_handler.Handle(s_command);

        private It _should_have_an_instance_name_before = () => s_commandProcessor.Observe<MonitorEvent>().InstanceName.ShouldEqual("");
        private It _should_post_the_event_type_to_the_control_bus_before = () => s_commandProcessor.Observe<MonitorEvent>().EventType.ShouldEqual(MonitorEventType.EnterHandler);
        private It _should_post_the_handler_name_to_the_control_bus_before = () => s_commandProcessor.Observe<MonitorEvent>().HandlerName.ShouldEqual("MyMonitoredHandler");
        private It _should_include_the_underlying_request_details_before = () => s_commandProcessor.Observe<MonitorEvent>().TargetHandlerRequest.ShouldNotBeNull();
        private It should_post_the_time_of_the_request_before = () => s_commandProcessor.Observe<MonitorEvent>().EventTime.ShouldEqual(s_at);

        Cleanup _tearDown = () =>
        {
            Clock.Clear();
            ConfigurationManager.AppSettings.Remove("IsMonitoringEnabled");
        };
    }
}
