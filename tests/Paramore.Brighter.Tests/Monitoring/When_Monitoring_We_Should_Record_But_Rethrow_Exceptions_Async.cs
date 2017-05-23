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
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;
using Paramore.Brighter.Monitoring.Configuration;
using Paramore.Brighter.Monitoring.Events;
using Paramore.Brighter.Monitoring.Handlers;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Tests.Monitoring.TestDoubles;
using Paramore.Brighter.Time;
using TinyIoC;

namespace Paramore.Brighter.Tests.Monitoring
{
    [Collection("Monitoring")]
    [Trait("Category", "Monitoring")]
    public class MonitorHandlerMustObserveButRethrowTests
    {
        private readonly MyCommand _command;
        private readonly SpyControlBusSender _controlBusSender;
        private readonly CommandProcessor _commandProcessor;
        private readonly string _originalRequestAsJson;
        private readonly DateTime _at;
        private MonitorEvent _afterEvent;
        private Exception _exception;

        public MonitorHandlerMustObserveButRethrowTests()
        {
            _controlBusSender = new SpyControlBusSender();
            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyMonitoredHandlerThatThrowsAsync>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactoryAsync(container);
            container.Register<IHandleRequestsAsync<MyCommand>, MyMonitoredHandlerThatThrowsAsync>();
            container.Register<IHandleRequestsAsync<MyCommand>, MonitorHandlerAsync<MyCommand>>();
            container.Register<IAmAControlBusSenderAsync>(_controlBusSender);
            container.Register(new MonitorConfiguration { IsMonitoringEnabled = true, InstanceName = "UnitTests" });

            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry());

            _command = new MyCommand();

            _originalRequestAsJson = JsonConvert.SerializeObject(_command);

            _at = DateTime.UtcNow;
            Clock.OverrideTime = _at;
        }

        [Fact]
        public async Task When_Monitoring_We_Should_Record_But_Rethrow_Exceptions_Async()
        {
            _exception = await Catch.ExceptionAsync(() => _commandProcessor.SendAsync(_command));
            _controlBusSender.Observe<MonitorEvent>();
            _afterEvent = _controlBusSender.Observe<MonitorEvent>();

           //_should_pass_through_the_exception_not_swallow
            _exception.Should().NotBeNull();
            //_should_monitor_the_exception
            _afterEvent.Exception.Should().BeOfType<Exception>();
            //_should_surface_the_error_message
            _afterEvent.Exception.Message.Should().Contain("monitored");
            //_should_have_an_instance_name_after
            _afterEvent.InstanceName.Should().Be("UnitTests");
            //_should_post_the_handler_fullname_to_the_control_bus_after
            _afterEvent.HandlerFullAssemblyName.Should().Be(typeof(MyMonitoredHandlerThatThrowsAsync).AssemblyQualifiedName);
            //_should_post_the_handler_name_to_the_control_bus_after
            _afterEvent.HandlerName.Should().Be(typeof(MyMonitoredHandlerThatThrowsAsync).FullName);
            //_should_include_the_underlying_request_details_after
            _afterEvent.RequestBody.Should().Be(_originalRequestAsJson);
            //should_post_the_time_of_the_request_after
            _afterEvent.EventTime.AsUtc().Should().BeAfter(_at.AsUtc());
            //should_post_the_elapsedtime_of_the_request_after
            _afterEvent.TimeElapsedMs.Should().Be((_afterEvent.EventTime.AsUtc() - _at.AsUtc()).Milliseconds);
        }
   }
}
