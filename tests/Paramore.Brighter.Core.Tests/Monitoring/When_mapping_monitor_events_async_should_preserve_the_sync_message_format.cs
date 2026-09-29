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
using TUnit.Assertions;
using TUnit.Core;

namespace Paramore.Brighter.Core.Tests.Monitoring;

[Category("Monitoring")]
public class MonitorEventMessageMapperAsyncTests
{
    [Test]
    [Arguments("monitoring.events")]
    [Arguments(null)]
    public async Task When_mapping_monitor_events_async_should_preserve_the_sync_message_format(string? topic)
    {
        //Arrange
        var mapper = new MonitorEventMessageMapper();
        await Assert.That(mapper).IsAssignableTo<IAmAMessageMapperAsync<MonitorEvent>>();
        var asyncMapper = (IAmAMessageMapperAsync<MonitorEvent>)mapper;
        var monitorEvent = new MonitorEvent("UnitTests", MonitorEventType.EnterHandler,
            "TestHandler", "TestHandler, TestAssembly", "{\"Value\":\"Hello\"}", DateTime.UtcNow, 34);
        var publication = new Publication { Topic = topic is null ? null : new RoutingKey(topic) };
        var syncMessage = mapper.MapToMessage(monitorEvent, publication);

        //Act
        var message = await asyncMapper.MapToMessageAsync(monitorEvent, publication);
        var restored = await asyncMapper.MapToRequestAsync(message);

        //Assert
        await Assert.That(message.Header.MessageId).IsEqualTo(syncMessage.Header.MessageId);
        await Assert.That(message.Header.Topic).IsEqualTo(syncMessage.Header.Topic);
        await Assert.That(message.Body.Value).IsEqualTo(syncMessage.Body.Value);
        await Assert.That(restored.Id).IsEqualTo(monitorEvent.Id);
        await Assert.That(restored.InstanceName).IsEqualTo(monitorEvent.InstanceName);
        await Assert.That(restored.EventType).IsEqualTo(monitorEvent.EventType);
        await Assert.That(restored.HandlerName).IsEqualTo(monitorEvent.HandlerName);
        await Assert.That(restored.HandlerFullAssemblyName).IsEqualTo(monitorEvent.HandlerFullAssemblyName);
        await Assert.That(restored.RequestBody).IsEqualTo(monitorEvent.RequestBody);
        await Assert.That(restored.EventTime).IsEqualTo(monitorEvent.EventTime);
        await Assert.That(restored.TimeElapsedMs).IsEqualTo(monitorEvent.TimeElapsedMs);
        await Assert.That(restored.Exception).IsNull();
    }
}
