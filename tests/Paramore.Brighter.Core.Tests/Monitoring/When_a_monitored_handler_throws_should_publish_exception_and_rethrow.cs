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

namespace Paramore.Brighter.Core.Tests.Monitoring;

[Property("Category", "Monitoring")]
public class MonitorControlBusExceptionTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task When_a_monitored_handler_throws_should_publish_exception_and_rethrow(bool isAsync)
    {
        //Arrange
        var bus = new InternalBus();
        var topic = new RoutingKey("monitoring.events");
        using var producer = new InMemoryMessageProducer(bus,
            new Publication { Topic = topic, RequestType = typeof(MonitorEvent) });
        using var producers = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
        {
            { topic, producer }
        });
        var sender = new ControlBusSenderFactory().Create<Message, CommittableTransaction>(
            new InMemoryOutbox(TimeProvider.System), producers, new BrighterTracer());
        await Assert.That(sender).IsAssignableTo<IDisposable>();
        using var senderLifetime = (IDisposable)sender;
        await Assert.That(sender).IsAssignableTo<IAmAControlBusSenderAsync>();
        var asyncSender = (IAmAControlBusSenderAsync)sender;

        var subscribers = new SubscriberRegistry();
        if (isAsync)
            subscribers.RegisterAsync<MyCommand, MyMonitoredHandlerThatThrowsAsync>();
        else
            subscribers.Register<MyCommand, MyMonitoredHandlerThatThrows>();
        var services = new ServiceCollection();
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
            new InMemorySchedulerFactory());
        var command = new MyCommand();
        var requestBody = JsonSerializer.Serialize(command, JsonSerialisationOptions.Options);
        var handlerType = isAsync ? typeof(MyMonitoredHandlerThatThrowsAsync) : typeof(MyMonitoredHandler);

        //Act
        var exception = isAsync
            ? await TestExceptionRecorder.CaptureAsync(() => processor.SendAsync(command))
            : TestExceptionRecorder.Capture(() => processor.Send(command));

        //Assert
        await Assert.That(exception).IsTypeOf<Exception>();
        await Assert.That(exception.Message).IsEqualTo("I am an exception in a monitored pipeline");
        await Assert.That(exception.StackTrace).Contains(
            isAsync ? nameof(MyMonitoredHandlerThatThrowsAsync) : nameof(MyMonitoredHandlerThatThrows));
        var messages = bus.Stream(topic).ToArray();
        var mapper = new MonitorEventMessageMapper();
        var events = messages.Select(mapper.MapToRequest).ToArray();
        await Assert.That(events.Select(e => e.EventType))
            .IsEquivalentTo(new[] { MonitorEventType.EnterHandler, MonitorEventType.ExceptionThrown },
                TUnit.Assertions.Enums.CollectionOrdering.Matching);
        foreach (var message in messages)
        {
            await Assert.That(message.Header.Topic).IsEqualTo(topic);
        }
        foreach (var monitorEvent in events)
        {
            await Assert.That(monitorEvent.InstanceName).IsEqualTo("UnitTests");
            await Assert.That(monitorEvent.HandlerName).IsEqualTo(handlerType.FullName);
            await Assert.That(monitorEvent.HandlerFullAssemblyName).IsEqualTo(handlerType.AssemblyQualifiedName);
            await Assert.That(monitorEvent.RequestBody).IsEqualTo(requestBody);
        }
        await Assert.That(events[0].Exception).IsNull();
        await Assert.That(events[1].Exception).IsNotNull();
        await Assert.That(events[1].Exception.Message).IsEqualTo(exception.Message);
        using var body = JsonDocument.Parse(messages[1].Body.Value);
        var details = body.RootElement.GetProperty("exception");
        await Assert.That(details.GetProperty("type").GetString()).IsEqualTo(exception.GetType().FullName);
        await Assert.That(details.GetProperty("message").GetString()).IsEqualTo(exception.Message);
        // The event captures the stack before the exception is rethrown to the caller.
        var recordedStackTrace = details.GetProperty("stackTrace").GetString();
        await Assert.That(string.IsNullOrEmpty(recordedStackTrace)).IsFalse();
        await Assert.That(exception.StackTrace).StartsWith(recordedStackTrace);
    }
}
