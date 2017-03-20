#region Licence
/* The MIT License (MIT)
Copyright � 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the �Software�), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED �AS IS�, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using Newtonsoft.Json;
using Xunit;
using Paramore.Brighter.Monitoring.Configuration;
using Paramore.Brighter.Monitoring.Events;
using Paramore.Brighter.Monitoring.Handlers;
using Paramore.Brighter.Tests.Monitoring.TestDoubles;
using Paramore.Brighter.Tests.TestDoubles;
using Paramore.Brighter.Time;
using TinyIoC;

namespace Paramore.Brighter.Tests.Monitoring
{
    public class MonitorHandlerPipelineTests
    {
        private MyCommand _command;
        private IAmACommandProcessor _commandProcessor;
        private SpyControlBusSender _controlBusSender;
        private DateTime _at;
        private MonitorEvent _beforeEvent;
        private MonitorEvent _afterEvent;
        private string _originalRequestAsJson;

        public MonitorHandlerPipelineTests()
        {
            _controlBusSender = new SpyControlBusSender();
            var registry = new SubscriberRegistry();
            
            registry.Register<MyCommand, MyMonitoredHandler>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactory(container);
            container.Register<IHandleRequests<MyCommand>, MyMonitoredHandler>();
            container.Register<IHandleRequests<MyCommand>, MonitorHandler<MyCommand>>();
            container.Register<IAmAControlBusSender>(_controlBusSender);
            container.Register<MonitorConfiguration>(new MonitorConfiguration {IsMonitoringEnabled = true, InstanceName = "UnitTests" });
            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry());

            _command = new MyCommand();

            _originalRequestAsJson = JsonConvert.SerializeObject(_command);

            _at = DateTime.UtcNow;
            Clock.OverrideTime = _at;
        }

        [Fact]
        public void When_Monitoring_Is_On_For_A_Handler()
        {
            _commandProcessor.Send(_command);
            _beforeEvent = _controlBusSender.Observe<MonitorEvent>();
            _afterEvent = _controlBusSender.Observe<MonitorEvent>();

            //_should_have_an_instance_name_before
            Assert.AreEqual("UnitTests", _beforeEvent.InstanceName);
            //_should_post_the_event_type_to_the_control_bus_before
            Assert.AreEqual(MonitorEventType.EnterHandler, _beforeEvent.EventType);
            //_should_post_the_handler_fullname_to_the_control_bus_before
            Assert.AreEqual(typeof(MyMonitoredHandler).AssemblyQualifiedName, _beforeEvent.HandlerFullAssemblyName);
            //_should_post_the_handler_name_to_the_control_bus_before
            Assert.AreEqual(typeof(MyMonitoredHandler).FullName, _beforeEvent.HandlerName);
            //_should_include_the_underlying_request_details_before
            Assert.AreEqual(_originalRequestAsJson, _beforeEvent.RequestBody);
            //_should_post_the_time_of_the_request_before
            Assert.AreEqual(_at, _beforeEvent.EventTime);
            //_should_elapsed_before_as_zero
            Assert.AreEqual(0, _beforeEvent.TimeElapsedMs);
            //_should_have_an_instance_name_after
            Assert.AreEqual("UnitTests", _afterEvent.InstanceName);
            //_should_post_the_handler_fullname_to_the_control_bus_after
            Assert.AreEqual(MonitorEventType.ExitHandler, _afterEvent.EventType);
            //_should_post_the_handler_fullname_to_the_control_bus_after
            Assert.AreEqual(typeof(MyMonitoredHandler).AssemblyQualifiedName, _afterEvent.HandlerFullAssemblyName);
            //_should_post_the_handler_name_to_the_control_bus_after
            Assert.AreEqual(typeof(MyMonitoredHandler).FullName, _afterEvent.HandlerName);
            //_should_include_the_underlying_request_details_after
            Assert.AreEqual(_originalRequestAsJson, _afterEvent.RequestBody);
            //should_post_the_time_of_the_request_after
            Assert.Greater(_afterEvent.EventTime, _at);
            //should_post_the_elapsedtime_of_the_request_after
            Assert.AreEqual((_afterEvent.EventTime - _beforeEvent.EventTime).Milliseconds, _afterEvent.TimeElapsedMs);
        }
   }
}