﻿#region Licence
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
using Paramore.Brighter.Core.Tests.Monitoring.TestDoubles;
using Xunit;
using Paramore.Brighter.Monitoring.Configuration;
using Paramore.Brighter.Monitoring.Events;
using Paramore.Brighter.Monitoring.Handlers;
using Polly.Registry;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using System.Text.Json;
using Paramore.Brighter.JsonConverters;

namespace Paramore.Brighter.Core.Tests.Monitoring
{
    [Trait("Category", "Monitoring")]
    public class MonitorHandlerPipelineTests : IDisposable
    {
        private readonly MyCommand _command;
        private readonly IAmACommandProcessor _commandProcessor;
        private readonly SpyControlBusSender _controlBusSender;
        private readonly DateTime _at;
        private readonly string _originalRequestAsJson;
        private MonitorEvent _beforeEvent;
        private MonitorEvent _afterEvent;

        public MonitorHandlerPipelineTests()
        {
            _controlBusSender = new SpyControlBusSender();
            var registry = new SubscriberRegistry();
            
            registry.Register<MyCommand, MyMonitoredHandler>();

            var container = new ServiceCollection();
            container.AddTransient<MyMonitoredHandler>();
            container.AddTransient<MonitorHandler<MyCommand>>();
            container.AddSingleton<IAmAControlBusSender>(_controlBusSender);
            container.AddSingleton(new MonitorConfiguration { IsMonitoringEnabled = true, InstanceName = "UnitTests" });
            container.AddSingleton<IBrighterOptions>(new BrighterOptions {HandlerLifetime = ServiceLifetime.Transient});
             
            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());

            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), new InMemorySchedulerFactory());

            _command = new MyCommand();

            _originalRequestAsJson = JsonSerializer.Serialize(_command, JsonSerialisationOptions.Options);

            _at = DateTime.UtcNow.AddMilliseconds(-500);
        }

        [Fact]
        public void When_Monitoring_Is_On_For_A_Handler()
        {
            _commandProcessor.Send(_command);
            _beforeEvent = _controlBusSender.Observe<MonitorEvent>();
            _afterEvent = _controlBusSender.Observe<MonitorEvent>();

            //_should_have_an_instance_name_before
            Assert.Equal("UnitTests", _beforeEvent.InstanceName);
            //_should_post_the_event_type_to_the_control_bus_before
            Assert.Equal(MonitorEventType.EnterHandler, _beforeEvent.EventType);
            //_should_post_the_handler_fullname_to_the_control_bus_before
            Assert.Equal(typeof(MyMonitoredHandler).AssemblyQualifiedName, _beforeEvent.HandlerFullAssemblyName);
            //_should_post_the_handler_name_to_the_control_bus_before
            Assert.Equal(typeof(MyMonitoredHandler).FullName, _beforeEvent.HandlerName);
            //_should_include_the_underlying_request_details_before
            Assert.Equal(_originalRequestAsJson, _beforeEvent.RequestBody);
            //_should_post_the_time_of_the_request_before
            Assert.True((_beforeEvent.EventTime.ToUniversalTime()) >= ((_at.ToUniversalTime()) - (TimeSpan.FromSeconds(1))) && (_beforeEvent.EventTime.ToUniversalTime()) <= ((_at.ToUniversalTime()) + (TimeSpan.FromSeconds(1))));
            //_should_elapsed_before_as_zero
            Assert.Equal(0, _beforeEvent.TimeElapsedMs);
            //_should_have_an_instance_name_after
            Assert.Equal("UnitTests", _afterEvent.InstanceName);
            //_should_post_the_handler_fullname_to_the_control_bus_after
            Assert.Equal(MonitorEventType.ExitHandler, _afterEvent.EventType);
            //_should_post_the_handler_fullname_to_the_control_bus_after
            Assert.Equal(typeof(MyMonitoredHandler).AssemblyQualifiedName, _afterEvent.HandlerFullAssemblyName);
            //_should_post_the_handler_name_to_the_control_bus_after
            Assert.Equal(typeof(MyMonitoredHandler).FullName, _afterEvent.HandlerName);
            //_should_include_the_underlying_request_details_after
            Assert.Equal(_originalRequestAsJson, _afterEvent.RequestBody);
            //should_post_the_time_of_the_request_after
            Assert.True((_afterEvent.EventTime.ToUniversalTime()) > (_at.ToUniversalTime()));
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
