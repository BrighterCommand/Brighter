using System;
using System.Text.Json;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Monitoring.Events;
using Paramore.Brighter.Monitoring.Mappers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Monitoring;

[Trait("Category", "Monitoring")]
public class MonitorEventMessageMapperNullTopicTests
{
    [Fact]
    public void When_monitor_event_message_mapper_maps_with_null_topic_it_should_use_routing_key_empty()
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
        Assert.Equal(RoutingKey.Empty, message.Header.Topic);
    }
}
