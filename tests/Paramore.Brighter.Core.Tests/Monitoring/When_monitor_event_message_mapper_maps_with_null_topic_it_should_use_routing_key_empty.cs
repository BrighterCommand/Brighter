using System;
using System.Text.Json;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Monitoring.Events;
using Paramore.Brighter.Monitoring.Mappers;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.Monitoring;

[Property("Category", "Monitoring")]
public class MonitorEventMessageMapperNullTopicTests
{
    [Test]
    public async Task When_monitor_event_message_mapper_maps_with_null_topic_it_should_use_routing_key_empty()
    {
        //Arrange
        var mapper = new MonitorEventMessageMapper();
        var originalRequestAsJson = JsonSerializer.Serialize(new MyCommand(), JsonSerialisationOptions.Options);
        var @event = new MonitorEvent(
            "TestInstance",
            MonitorEventType.EnterHandler,
            "TestHandler",
            "TestHandler, TestAssembly",
            originalRequestAsJson,
            DateTime.UtcNow,
            100);

        //Act
        var message = mapper.MapToMessage(@event, new Publication { Topic = null });

        //Assert
        await Assert.That(message.Header.Topic).IsEqualTo(RoutingKey.Empty);
    }
}