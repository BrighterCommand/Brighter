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
using Machine.Specifications;
using Newtonsoft.Json;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.monitoring.Events;
using paramore.brighter.commandprocessor.monitoring.Mappers;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;

namespace paramore.commandprocessor.tests.Monitoring
{
    [Subject(typeof(MonitorEventMessageMapper))]
    public class When_Serializing_A_Monitoring_Event
    {
        private const string InstanceName = "Paramore.Tests";
        private const string HandlerFullAssemblyName = "Paramore.Dummy.Handler, with some Assembly information";
        private const string HandlerName = "Paramore.Dummy.Handler";
        private static MonitorEventMessageMapper s_monitorEventMessageMapper;
        private static MonitorEvent s_monitorEvent;
        private static Message s_message;
        private static string s_originalRequestAsJson;

        private Establish context = () =>
        {
            _overrideTime = DateTime.UtcNow;
            Clock.OverrideTime = _overrideTime;

            s_monitorEventMessageMapper = new MonitorEventMessageMapper();

            s_originalRequestAsJson = JsonConvert.SerializeObject(new MyCommand());
            _elapsedMilliseconds = 34;
            var @event = new MonitorEvent(InstanceName, MonitorEventType.EnterHandler, HandlerName, HandlerFullAssemblyName, s_originalRequestAsJson, Clock.Now().Value, _elapsedMilliseconds);
            s_message = s_monitorEventMessageMapper.MapToMessage(@event);

        };

        private Because of = () => s_monitorEvent = s_monitorEventMessageMapper.MapToRequest(s_message);

        private It _should_have_the_correct_instance_name = () => s_monitorEvent.InstanceName.ShouldEqual(InstanceName);
        private It _should_have_the_correct_handler_name = () => s_monitorEvent.HandlerName.ShouldEqual(HandlerName);
        private It _should_have_the_correct_handler_full_assembly_name = () => s_monitorEvent.HandlerFullAssemblyName.ShouldEqual(HandlerFullAssemblyName);
        private It _should_have_the_correct_monitor_type = () => s_monitorEvent.EventType.ShouldEqual(MonitorEventType.EnterHandler);
        private It _should_have_the_original_request_as_json = () => s_monitorEvent.RequestBody.ShouldEqual(s_originalRequestAsJson);
        private It _should_have_the_correct_event_time = () => s_monitorEvent.EventTime.ShouldEqual(_overrideTime);
        private It _should_have_the_correct_time_elapsed = () => s_monitorEvent.TimeElapsedMs.ShouldEqual(_elapsedMilliseconds);
        private static int _elapsedMilliseconds;
        private static DateTime _overrideTime;
    }
}
