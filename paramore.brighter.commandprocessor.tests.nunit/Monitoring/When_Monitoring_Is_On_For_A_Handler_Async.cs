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
using Newtonsoft.Json;
using Nito.AsyncEx;
using NUnit.Framework;
using paramore.brighter.commandprocessor.monitoring.Configuration;
using paramore.brighter.commandprocessor.monitoring.Events;
using paramore.brighter.commandprocessor.monitoring.Handlers;
using paramore.brighter.commandprocessor.tests.nunit.CommandProcessors.TestDoubles;
using paramore.brighter.commandprocessor.tests.nunit.Monitoring.TestDoubles;
using paramore.brighter.commandprocessor.time;
using TinyIoC;

namespace paramore.brighter.commandprocessor.tests.nunit.Monitoring
{
    [TestFixture]
    public class MonitorHandlerPipelineAsyncTests
    {
        private MyCommand _command;
        private IAmACommandProcessor _commandProcessor;
        private SpyControlBusSender _controlBusSender;
        private DateTime _at;
        private MonitorEvent _beforeEvent;
        private MonitorEvent _afterEvent;
        private string _originalRequestAsJson;

        [SetUp]
        public void Establish()
        {
            _controlBusSender = new SpyControlBusSender();
            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyMonitoredHandlerAsync>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactoryAsync(container);
            container.Register<IHandleRequestsAsync<MyCommand>, MyMonitoredHandlerAsync>();
            container.Register<IHandleRequestsAsync<MyCommand>, MonitorHandlerAsync<MyCommand>>();
            container.Register<IAmAControlBusSenderAsync>(_controlBusSender);
            container.Register<MonitorConfiguration>(new MonitorConfiguration { IsMonitoringEnabled = true, InstanceName = "UnitTests" });

            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry());

            _command = new MyCommand();

            _originalRequestAsJson = JsonConvert.SerializeObject(_command);

            _at = DateTime.UtcNow;
            Clock.OverrideTime = _at;
        }

        [Test]
        public void When_Monitoring_Is_on_For_A_Handler_Async()
        {
            AsyncContext.Run(async () => await _commandProcessor.SendAsync(_command));
            _beforeEvent = _controlBusSender.Observe<MonitorEvent>();
            _afterEvent = _controlBusSender.Observe<MonitorEvent>();


            //_should_have_an_instance_name_before
            _beforeEvent.InstanceName.ShouldEqual("UnitTests"); //set in the config
            //_should_post_the_event_type_to_the_control_bus_before
            _beforeEvent.EventType.ShouldEqual(MonitorEventType.EnterHandler);
            //_should_post_the_handler_fullname_to_the_control_bus_before
            _beforeEvent.HandlerFullAssemblyName.ShouldEqual(typeof(MyMonitoredHandlerAsync).AssemblyQualifiedName);
            //_should_post_the_handler_name_to_the_control_bus_before
            _beforeEvent.HandlerName.ShouldEqual(typeof(MyMonitoredHandlerAsync).FullName);
            //_should_include_the_underlying_request_details_before
            _beforeEvent.RequestBody.ShouldEqual(_originalRequestAsJson);
            //should_post_the_time_of_the_request_before
            _beforeEvent.EventTime.ShouldEqual(_at);
            //_should_have_an_instance_name_after
            _afterEvent.InstanceName.ShouldEqual("UnitTests");   //set in the config
            //_should_post_the_event_type_to_the_control_bus_after
            _afterEvent.EventType.ShouldEqual(MonitorEventType.ExitHandler);
            //_should_post_the_handler_fullname_to_the_control_bus_after
            _afterEvent.HandlerFullAssemblyName.ShouldEqual(typeof(MyMonitoredHandlerAsync).AssemblyQualifiedName);
            //_should_post_the_handler_name_to_the_control_bus_after
            _afterEvent.HandlerName.ShouldEqual(typeof(MyMonitoredHandlerAsync).FullName);
            //_should_include_the_underlying_request_details_after
            _afterEvent.RequestBody.ShouldEqual(_originalRequestAsJson);
            //should_post_the_time_of_the_request_after
            _afterEvent.EventTime.ShouldBeGreaterThan(_at);
            //should_post_the_elapsedtime_of_the_request_after
            _afterEvent.TimeElapsedMs.ShouldEqual((_afterEvent.EventTime - _beforeEvent.EventTime).Milliseconds);
        }
   }
}
