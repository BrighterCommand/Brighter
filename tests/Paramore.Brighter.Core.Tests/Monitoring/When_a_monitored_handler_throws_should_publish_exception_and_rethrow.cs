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
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.Monitoring.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Monitoring.Configuration;
using Paramore.Brighter.Monitoring.Events;
using Paramore.Brighter.Monitoring.Handlers;
using Paramore.Brighter.Monitoring.Mappers;
using Paramore.Brighter.Observability;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Monitoring;

[Trait("Category", "Monitoring")]
public class MonitorControlBusExceptionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_a_monitored_handler_throws_should_publish_exception_and_rethrow(bool isAsync)
    {
        //Arrange
        var bus = new InternalBus();
        var topic = new RoutingKey("monitoring.events");
        using var producer = new InMemoryMessageProducer(bus, Initializer.TestLoggerFactory,
            new Publication { Topic = topic, RequestType = typeof(MonitorEvent) });
        using var producers = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
        {
            { topic, producer }
        });
        var sender = new ControlBusSenderFactory(Initializer.TestLoggerFactory).Create<Message, CommittableTransaction>(
            new InMemoryOutbox(TimeProvider.System), producers, new BrighterTracer());
        using var senderLifetime = Assert.IsAssignableFrom<IDisposable>(sender);
        var asyncSender = Assert.IsAssignableFrom<IAmAControlBusSenderAsync>(sender);

        var subscribers = new SubscriberRegistry();
        if (isAsync)
            subscribers.RegisterAsync<MyCommand, MyMonitoredHandlerThatThrowsAsync>();
        else
            subscribers.Register<MyCommand, MyMonitoredHandlerThatThrows>();
        var services = new ServiceCollection().AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory>(Initializer.TestLoggerFactory);
        services.AddTransient<MyMonitoredHandlerThatThrows>();
        services.AddTransient<MyMonitoredHandlerThatThrowsAsync>();
        services.AddTransient<MonitorHandler<MyCommand>>();
        services.AddTransient<MonitorHandlerAsync<MyCommand>>();
        services.AddSingleton(sender);
        services.AddSingleton(asyncSender);
        services.AddSingleton(new MonitorConfiguration { IsMonitoringEnabled = true, InstanceName = "UnitTests" });
        services.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });
        using var provider = services.BuildServiceProvider();
        var processor = new CommandProcessor(subscribers, new ServiceProviderHandlerFactory(provider),
            new InMemoryRequestContextFactory(), new PolicyRegistry(), new ResiliencePipelineRegistry<string>(),
            new InMemorySchedulerFactory(Initializer.TestLoggerFactory), Initializer.TestLoggerFactory);
        var command = new MyCommand();
        var requestBody = JsonSerializer.Serialize(command, JsonSerialisationOptions.Options);
        var handlerType = isAsync ? typeof(MyMonitoredHandlerThatThrowsAsync) : typeof(MyMonitoredHandler);

        //Act
        var exception = isAsync
            ? await Record.ExceptionAsync(() => processor.SendAsync(command))
            : Record.Exception(() => processor.Send(command));

        //Assert
        Assert.IsType<Exception>(exception);
        Assert.Equal("I am an exception in a monitored pipeline", exception.Message);
        Assert.Contains(isAsync ? nameof(MyMonitoredHandlerThatThrowsAsync) : nameof(MyMonitoredHandlerThatThrows),
            exception.StackTrace);
        var messages = bus.Stream(topic).ToArray();
        var mapper = new MonitorEventMessageMapper();
        var events = messages.Select(mapper.MapToRequest).ToArray();
        Assert.Equal(new[] { MonitorEventType.EnterHandler, MonitorEventType.ExceptionThrown },
            events.Select(e => e.EventType));
        Assert.All(messages, message => Assert.Equal(topic, message.Header.Topic));
        Assert.All(events, monitorEvent =>
        {
            Assert.Equal("UnitTests", monitorEvent.InstanceName);
            Assert.Equal(handlerType.FullName, monitorEvent.HandlerName);
            Assert.Equal(handlerType.AssemblyQualifiedName, monitorEvent.HandlerFullAssemblyName);
            Assert.Equal(requestBody, monitorEvent.RequestBody);
        });
        Assert.Null(events[0].Exception);
        Assert.NotNull(events[1].Exception);
        Assert.Equal(exception.Message, events[1].Exception.Message);
        using var body = JsonDocument.Parse(messages[1].Body.Value);
        var details = body.RootElement.GetProperty("exception");
        Assert.Equal(exception.GetType().FullName, details.GetProperty("type").GetString());
        Assert.Equal(exception.Message, details.GetProperty("message").GetString());
        // The event captures the stack before the exception is rethrown to the caller.
        var recordedStackTrace = details.GetProperty("stackTrace").GetString();
        Assert.False(string.IsNullOrEmpty(recordedStackTrace));
        Assert.StartsWith(recordedStackTrace, exception.StackTrace);
    }
}
