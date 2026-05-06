#region Licence
/* The MIT License (MIT)
Copyright © 2015 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.Monitoring.TestDoubles;
using Paramore.Brighter.Monitoring.Configuration;
using Paramore.Brighter.Monitoring.Events;
using Paramore.Brighter.Monitoring.Handlers;
using Polly.Registry;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using System.Text.Json;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.JsonConverters;

namespace Paramore.Brighter.Core.Tests.Monitoring
{
    [Property("Category", "Monitoring")]
    public class MonitorHandlerMustObserveButRethrowTests
    {
        private readonly MyCommand _command;
        private readonly SpyControlBusSender _controlBusSender;
        private readonly CommandProcessor _commandProcessor;
        private readonly string _originalRequestAsJson;
        private readonly DateTime _at;
        private MonitorEvent _afterEvent;
        private Exception? _exception;
        public MonitorHandlerMustObserveButRethrowTests()
        {
            _controlBusSender = new SpyControlBusSender();
            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyMonitoredHandlerThatThrowsAsync>();
            var container = new ServiceCollection();
            container.AddTransient<MyMonitoredHandlerThatThrowsAsync>();
            container.AddTransient<MonitorHandlerAsync<MyCommand>>();
            container.AddSingleton<IAmAControlBusSenderAsync>(_controlBusSender);
            container.AddSingleton(new MonitorConfiguration { IsMonitoringEnabled = true, InstanceName = "UnitTests" });
            container.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });
            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());
            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
            _command = new MyCommand();
            _originalRequestAsJson = JsonSerializer.Serialize(_command, JsonSerialisationOptions.Options);
            _at = DateTime.UtcNow.AddMilliseconds(-500);
        }

        [Test]
        public async Task When_Monitoring_We_Should_Record_But_Rethrow_Exceptions_Async()
        {
            _exception = await Catch.ExceptionAsync(() => _commandProcessor.SendAsync(_command));
            _controlBusSender.Observe<MonitorEvent>();
            _afterEvent = _controlBusSender.Observe<MonitorEvent>();
            //_should_pass_through_the_exception_not_swallow
            await Assert.That(_exception).IsNotNull();
            //_should_monitor_the_exception
            await Assert.That((_afterEvent.Exception) is Exception).IsTrue();
            //_should_surface_the_error_message
            await Assert.That(_afterEvent.Exception.Message).Contains("monitored");
            //_should_have_an_instance_name_after
            await Assert.That(_afterEvent.InstanceName).IsEqualTo("UnitTests");
            //_should_post_the_handler_fullname_to_the_control_bus_after
            await Assert.That(_afterEvent.HandlerFullAssemblyName).IsEqualTo(typeof(MyMonitoredHandlerThatThrowsAsync).AssemblyQualifiedName);
            //_should_post_the_handler_name_to_the_control_bus_after
            await Assert.That(_afterEvent.HandlerName).IsEqualTo(typeof(MyMonitoredHandlerThatThrowsAsync).FullName);
            //_should_include_the_underlying_request_details_after
            await Assert.That(_afterEvent.RequestBody).IsEqualTo(_originalRequestAsJson);
            //should_post_the_time_of_the_request_after
            await Assert.That((_afterEvent.EventTime.ToUniversalTime()) > (_at.ToUniversalTime())).IsTrue();
        }
    }
}