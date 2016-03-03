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
using FakeItEasy;
using Machine.Specifications;
using Newtonsoft.Json;
using Nito.AsyncEx;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.monitoring.Events;
using paramore.brighter.commandprocessor.monitoring.Handlers;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;
using paramore.commandprocessor.tests.Monitoring.TestDoubles;
using TinyIoC;

namespace paramore.commandprocessor.tests.Monitoring
{
    [Subject(typeof(MonitorHandler<>))]
    public class When_Monitoring_We_Should_Record_But_Rethrow_Exceptions_Async
    {
        private static MyCommand s_command;
        private static Exception s_thrownException;
        private static SpyControlBusSender s_controlBusSender;
        private static CommandProcessor s_commandProcessor;
        private static MonitorEvent s_afterEvent;
        private static string s_originalRequestAsJson;
        private static DateTime s_at;

        private Establish _context = () => 
        {
            var logger = A.Fake<ILog>();
            s_controlBusSender = new SpyControlBusSender();
            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyMonitoredHandlerThatThrowsAsync>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactoryAsync(container);
            container.Register<IHandleRequestsAsync<MyCommand>, MyMonitoredHandlerThatThrowsAsync>();
            container.Register<IHandleRequestsAsync<MyCommand>, MonitorHandlerAsync<MyCommand>>();
            container.Register<ILog>(logger);
            container.Register<IAmAControlBusSenderAsync>(s_controlBusSender);

            s_commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), logger);

            s_command = new MyCommand();

            s_originalRequestAsJson = JsonConvert.SerializeObject(s_command);

            s_at = DateTime.UtcNow;
            Clock.OverrideTime = s_at;
        };

        private Because _of = () =>
        {
            s_thrownException = Catch.Exception(() => AsyncContext.Run(async () => await s_commandProcessor.SendAsync(s_command)));
            Task.Delay(TimeSpan.FromSeconds(1)).Wait();
            s_controlBusSender.Observe<MonitorEvent>(); //pop but don't inspect before
            s_afterEvent = s_controlBusSender.Observe<MonitorEvent>();
        };

        private It _should_pass_through_the_exception_not_swallow = () => s_thrownException.ShouldNotBeNull();
        private It _should_monitor_the_exception = () => s_afterEvent.Exception.ShouldBeOfExactType(typeof(ApplicationException));
        private It _should_surface_the_error_message = () => s_afterEvent.Exception.Message.ShouldContain("monitored");
        private It _should_have_an_instance_name_after = () => s_afterEvent.InstanceName.ShouldEqual("UnitTests");   //set in the config
        private It _should_post_the_handler_name_to_the_control_bus_after = () => s_afterEvent.HandlerName.ShouldEqual(typeof(MyMonitoredHandlerThatThrowsAsync).AssemblyQualifiedName);
        private It _should_include_the_underlying_request_details_after = () => s_afterEvent.RequestBody.ShouldEqual(s_originalRequestAsJson);
        private It should_post_the_time_of_the_request_after = () => s_afterEvent.EventTime.ShouldEqual(s_at);
    }
}
