using System;
using Machine.Specifications;
using Newtonsoft.Json;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.monitoring.Events;
using paramore.brighter.commandprocessor.monitoring.Mappers;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;

namespace paramore.commandprocessor.tests.Monitoring
{
    public class When_serializing_a_monitoring_event
    {
        private const string InstanceName = "Paramore.Tests";
        private const string HandlerName = "Paramore.Dummy.Handler";
        private static MonitorEventMessageMapper s_monitorEventMessageMapper;
        private static MonitorEvent s_monitorEvent;
        private static Message s_message;
        private static string s_originalRequestAsJson;

        private Establish context = () =>
        {
            Clock.OverrideTime = DateTime.UtcNow;

            s_monitorEventMessageMapper = new MonitorEventMessageMapper();

            s_originalRequestAsJson = JsonConvert.SerializeObject(new MyCommand());
            var @event = new MonitorEvent(InstanceName, MonitorEventType.EnterHandler, HandlerName, s_originalRequestAsJson, Clock.Now().Value);
            s_message = s_monitorEventMessageMapper.MapToMessage(@event);

        };

        private Because of = () => s_monitorEvent = s_monitorEventMessageMapper.MapToRequest(s_message);

        private It _should_have_the_correct_instance_name = () => s_monitorEvent.InstanceName.ShouldEqual(InstanceName);
        private It _should_have_the_correct_handler_name = () => s_monitorEvent.HandlerName.ShouldEqual(HandlerName);
        private It _should_have_the_correct_monitor_type = () => s_monitorEvent.EventType.ShouldEqual(MonitorEventType.EnterHandler);
        private It _should_have_the_original_request_as_json = () => s_monitorEvent.TargetHandlerRequestJson.ShouldEqual(s_originalRequestAsJson);
        private It _should_have_the_correct_event_time = () => s_monitorEvent.EventTime.ShouldEqual(Clock.Now().Value);
    }
}
