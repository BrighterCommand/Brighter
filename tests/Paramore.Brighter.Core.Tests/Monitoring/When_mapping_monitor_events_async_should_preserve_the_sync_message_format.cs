#region Licence
/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

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

#nullable enable

using System;
using System.Threading.Tasks;
using Paramore.Brighter.Monitoring.Events;
using Paramore.Brighter.Monitoring.Mappers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Monitoring;

[Trait("Category", "Monitoring")]
public class MonitorEventMessageMapperAsyncTests
{
    [Theory]
    [InlineData("monitoring.events")]
    [InlineData(null)]
    public async Task When_mapping_monitor_events_async_should_preserve_the_sync_message_format(string? topic)
    {
        //Arrange
        var mapper = new MonitorEventMessageMapper();
        var asyncMapper = Assert.IsAssignableFrom<IAmAMessageMapperAsync<MonitorEvent>>(mapper);
        var monitorEvent = new MonitorEvent("UnitTests", MonitorEventType.EnterHandler,
            "TestHandler", "TestHandler, TestAssembly", "{\"Value\":\"Hello\"}", DateTime.UtcNow, 34);
        var publication = new Publication { Topic = topic is null ? null : new RoutingKey(topic) };
        var syncMessage = mapper.MapToMessage(monitorEvent, publication);

        //Act
        var message = await asyncMapper.MapToMessageAsync(monitorEvent, publication);
        var restored = await asyncMapper.MapToRequestAsync(message);

        //Assert
        Assert.Equal(syncMessage.Header.MessageId, message.Header.MessageId);
        Assert.Equal(syncMessage.Header.Topic, message.Header.Topic);
        Assert.Equal(syncMessage.Body.Value, message.Body.Value);
        Assert.Equal(monitorEvent.Id, restored.Id);
        Assert.Equal(monitorEvent.InstanceName, restored.InstanceName);
        Assert.Equal(monitorEvent.EventType, restored.EventType);
        Assert.Equal(monitorEvent.HandlerName, restored.HandlerName);
        Assert.Equal(monitorEvent.HandlerFullAssemblyName, restored.HandlerFullAssemblyName);
        Assert.Equal(monitorEvent.RequestBody, restored.RequestBody);
        Assert.Equal(monitorEvent.EventTime, restored.EventTime);
        Assert.Equal(monitorEvent.TimeElapsedMs, restored.TimeElapsedMs);
        Assert.Null(restored.Exception);
    }
}
