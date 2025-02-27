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
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Xunit;
using Paramore.Brighter.Monitoring.Events;
using Paramore.Brighter.Monitoring.Mappers;
using System.Text.Json;

namespace Paramore.Brighter.Core.Tests.Monitoring
{
    [Trait("Category", "Monitoring")]
    public class MonitorEventMessageMapperTests 
    {
        private const string InstanceName = "Paramore.Tests";
        private const string HandlerFullAssemblyName = "Paramore.Dummy.Handler, with some Assembly information";
        private const string HandlerName = "Paramore.Dummy.Handler";
        private MonitorEvent _monitorEvent;
        private readonly MonitorEventMessageMapper _monitorEventMessageMapper;
        private readonly Message _message;
        private readonly string _originalRequestAsJson;
        private static int _elapsedMilliseconds;
        private static DateTime _at;

        public MonitorEventMessageMapperTests()
        {
            _at = DateTime.UtcNow.AddMilliseconds(-500);

            _monitorEventMessageMapper = new MonitorEventMessageMapper();

            _originalRequestAsJson = JsonSerializer.Serialize(new MyCommand(), JsonSerialisationOptions.Options);
            _elapsedMilliseconds = 34;
            
            var @event = new MonitorEvent(InstanceName, MonitorEventType.EnterHandler, HandlerName, HandlerFullAssemblyName, _originalRequestAsJson, _at, _elapsedMilliseconds);
            _message = _monitorEventMessageMapper.MapToMessage(@event, new Publication{Topic = new RoutingKey("paramore.monitoring.event")});
       }

        [Fact]
        public void When_Serializing_A_Monitoring_Event()
        {
            _monitorEvent = _monitorEventMessageMapper.MapToRequest(_message);

            //Should have the correct instance name
            Assert.Equal(InstanceName, _monitorEvent.InstanceName);
            //Should have the correct handler name
            Assert.Equal(HandlerName, _monitorEvent.HandlerName);
            //Should have the correct handler full assembly name
            Assert.Equal(HandlerFullAssemblyName, _monitorEvent.HandlerFullAssemblyName);
            //Should have the correct monitor type
            Assert.Equal(MonitorEventType.EnterHandler, _monitorEvent.EventType);
            //Should have the original request as json
            Assert.Equal(_originalRequestAsJson, _monitorEvent.RequestBody);
            //Should have the correct event time
            Assert.True((_monitorEvent.EventTime.ToUniversalTime()) >= ((_at.ToUniversalTime()) - (TimeSpan.FromSeconds(1))) && (_monitorEvent.EventTime.ToUniversalTime()) <= ((_at.ToUniversalTime()) + (TimeSpan.FromSeconds(1))));
            //Should have the correct time elapsed
            Assert.Equal(_elapsedMilliseconds, _monitorEvent.TimeElapsedMs);
        }

   }
}
