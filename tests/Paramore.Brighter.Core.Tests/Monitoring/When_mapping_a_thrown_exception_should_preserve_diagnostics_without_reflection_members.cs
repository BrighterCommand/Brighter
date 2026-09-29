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

using System;
using System.Text.Json;
using System.Threading.Tasks;
using Paramore.Brighter.Monitoring.Events;
using Paramore.Brighter.Monitoring.Mappers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Monitoring;

[Trait("Category", "Monitoring")]
public class MonitorExceptionMappingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_mapping_a_thrown_exception_should_preserve_diagnostics_without_reflection_members(bool isAsync)
    {
        //Arrange
        Action throwException = () =>
            throw new InvalidOperationException("Handler failed", new ArgumentException("Invalid request"));
        var exception = Assert.Throws<InvalidOperationException>(throwException);
        exception.Data["unsupported"] = typeof(MonitorExceptionMappingTests);
        var monitorEvent = new MonitorEvent("UnitTests", MonitorEventType.ExceptionThrown,
            "TestHandler", "TestHandler, TestAssembly", "{}", DateTime.UtcNow, 10, exception);
        var mapper = new MonitorEventMessageMapper();
        var publication = new Publication { Topic = new RoutingKey("monitoring.events") };

        //Act
        var message = isAsync
            ? await mapper.MapToMessageAsync(monitorEvent, publication)
            : mapper.MapToMessage(monitorEvent, publication);
        var restored = isAsync
            ? await mapper.MapToRequestAsync(message)
            : mapper.MapToRequest(message);

        //Assert
        using var body = JsonDocument.Parse(message.Body.Value);
        var details = body.RootElement.GetProperty("exception");
        Assert.Equal(typeof(InvalidOperationException).FullName, details.GetProperty("type").GetString());
        Assert.Equal(exception.Message, details.GetProperty("message").GetString());
        Assert.Equal(exception.StackTrace, details.GetProperty("stackTrace").GetString());
        Assert.Equal(typeof(ArgumentException).FullName, details.GetProperty("innerException").GetProperty("type").GetString());
        Assert.Equal(exception.InnerException.Message, details.GetProperty("innerException").GetProperty("message").GetString());
        Assert.False(details.TryGetProperty("targetSite", out _));
        Assert.False(details.TryGetProperty("data", out _));
        Assert.Same(exception, monitorEvent.Exception);
        Assert.IsType<Exception>(restored.Exception);
        Assert.Equal(exception.Message, restored.Exception.Message);
        Assert.IsType<Exception>(restored.Exception.InnerException);
        Assert.Equal(exception.InnerException.Message, restored.Exception.InnerException.Message);
        Assert.Equal(monitorEvent.Id, restored.Id);
        Assert.Equal(monitorEvent.EventType, restored.EventType);
        Assert.Equal(monitorEvent.RequestBody, restored.RequestBody);
    }
}
