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
using FakeItEasy;
using Machine.Specifications;
using Newtonsoft.Json;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.monitoring.Events;
using paramore.brighter.commandprocessor.monitoring.Handlers;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;
using paramore.commandprocessor.tests.Monitoring.TestDoubles;
using TinyIoC;

namespace paramore.commandprocessor.tests.Monitoring
{
    [Subject("Monitoring")]
    public class When_monitoring_is_on_for_a_handler
    {
        private static MyCommand s_command;
        private static IAmACommandProcessor s_commandProcessor;
        private static SpyControlBusSender s_controlBusSender;
        private static DateTime s_at;
        private static MonitorEvent beforeEvent;
        private static MonitorEvent afterEvent;
        private static string s_originalRequestAsJson;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();
            s_controlBusSender = new SpyControlBusSender();
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyMonitoredHandler>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactory(container);
            container.Register<IHandleRequests<MyCommand>, MyMonitoredHandler>();
            container.Register<IHandleRequests<MyCommand>, MonitorHandler<MyCommand>>();
            container.Register<ILog>(logger);
            container.Register<IAmAControlBusSender>(s_controlBusSender);

            s_commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), logger);

            s_command = new MyCommand();

            s_originalRequestAsJson = JsonConvert.SerializeObject(s_command);

            s_at = DateTime.UtcNow;
            Clock.OverrideTime = s_at;
        };

        private Because _of = () =>
        {
            s_commandProcessor.Send(s_command);
            beforeEvent = s_controlBusSender.Observe<MonitorEvent>();
            afterEvent = s_controlBusSender.Observe<MonitorEvent>();
        };

        private It _should_have_an_instance_name_before = () => beforeEvent.InstanceName.ShouldEqual("UnitTests"); //set in the config
        private It _should_post_the_event_type_to_the_control_bus_before = () => beforeEvent.EventType.ShouldEqual(MonitorEventType.EnterHandler);
        private It _should_post_the_handler_name_to_the_control_bus_before = () => beforeEvent.HandlerName.ShouldEqual(typeof(MyMonitoredHandler).AssemblyQualifiedName);
        private It _should_include_the_underlying_request_details_before = () => beforeEvent.RequestBody.ShouldEqual(s_originalRequestAsJson);
        private It should_post_the_time_of_the_request_before = () => beforeEvent.EventTime.ShouldEqual(s_at);
        private It _should_have_an_instance_name_after = () => afterEvent.InstanceName.ShouldEqual("UnitTests");   //set in the config
        private It _should_post_the_event_type_to_the_control_bus_after= () => afterEvent.EventType.ShouldEqual(MonitorEventType.ExitHandler);
        private It _should_post_the_handler_name_to_the_control_bus_after = () => afterEvent.HandlerName.ShouldEqual(typeof(MyMonitoredHandler).AssemblyQualifiedName);
        private It _should_include_the_underlying_request_details_after = () => afterEvent.RequestBody.ShouldEqual(s_originalRequestAsJson);
        private It should_post_the_time_of_the_request_after = () => afterEvent.EventTime.ShouldEqual(s_at);

        Cleanup _tearDown = () =>
        {
            Clock.Clear();
        };

    }


    [Ignore("placeholder")]
    [Subject("Monitoring")]
    public class When_monitoring_we_should_record_but_rethrow_exceptions {}
}
