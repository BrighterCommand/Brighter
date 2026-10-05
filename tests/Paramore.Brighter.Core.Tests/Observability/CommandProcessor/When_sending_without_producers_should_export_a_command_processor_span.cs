#region Licence

/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia <irakli.gabisonia94@gmail.com>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

#nullable enable

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Diagnostics;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.CommandProcessor;

[Collection("Observability")]
public class CommandProcessorWithoutProducersTracingTests
{
    private readonly List<Activity> _exportedActivities = [];
    private readonly Dictionary<string, string> _receivedMessages = [];

    private ServiceProvider BuildServiceProvider(bool isAsync)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDictionary<string, string>>(_receivedMessages);
        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing.AddBrighterInstrumentation().AddInMemoryExporter(_exportedActivities));
        var registry = new ServiceCollectionSubscriberRegistry(services);
        if (isAsync)
        {
            registry.RegisterAsync<MyCommand, MyCommandHandlerAsync>();
            registry.RegisterAsync<MyEvent, MyEventHandlerAsync>();
        }
        else
        {
            registry.Register<MyCommand, MyCommandHandler>();
            registry.Register<MyEvent, MyEventHandler>();
        }

        services.AddSingleton<IAmACommandProcessor>(provider => CommandProcessorBuilder.StartNew()
            .Handlers(new HandlerConfiguration(registry, new ServiceProviderHandlerFactory(provider)))
            .DefaultResilience()
            .NoExternalBus()
            .ConfigureInstrumentation(provider.GetRequiredService<IAmABrighterTracer>(), InstrumentationOptions.RequestInformation)
            .RequestContextFactory(new InMemoryRequestContextFactory())
            .RequestSchedulerFactory(new InMemorySchedulerFactory())
            .Build());

        return services.BuildServiceProvider();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_sending_without_producers_should_export_a_command_processor_span(bool isAsync)
    {
        // Arrange
        using ServiceProvider provider = BuildServiceProvider(isAsync);
        TracerProvider tracing = provider.GetRequiredService<TracerProvider>();
        using IAmABrighterTracer tracer = provider.GetRequiredService<IAmABrighterTracer>();
        IAmACommandProcessor processor = provider.GetRequiredService<IAmACommandProcessor>();
        var command = new MyCommand();
        using Activity parent = new Activity("send without producers").SetIdFormat(ActivityIdFormat.W3C);
        parent.ActivityTraceFlags = ActivityTraceFlags.Recorded;
        parent.Start();
        var context = new RequestContext { Span = parent };

        // Act
        if (isAsync)
            await processor.SendAsync(command, context);
        else
            processor.Send(command, context);

        // Assert
        string handlerName = isAsync ? nameof(MyCommandHandlerAsync) : nameof(MyCommandHandler);
        Assert.Equal(command.Id.Value, _receivedMessages[handlerName]);
        Assert.True(tracing.ForceFlush());
        Activity span = Assert.Single(_exportedActivities, activity =>
            activity.Source.Name == BrighterSemanticConventions.SourceName
            && Equals(activity.GetTagItem(BrighterSemanticConventions.RequestId), command.Id.Value));
        Assert.Equal($"{nameof(MyCommand)} send", span.DisplayName);
        Assert.Equal(ActivityKind.Internal, span.Kind);
        Assert.Equal(parent.TraceId, span.TraceId);
        Assert.Equal(parent.SpanId, span.ParentSpanId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_publishing_without_producers_should_export_command_processor_and_handler_spans(bool isAsync)
    {
        // Arrange
        using ServiceProvider provider = BuildServiceProvider(isAsync);
        TracerProvider tracing = provider.GetRequiredService<TracerProvider>();
        using IAmABrighterTracer tracer = provider.GetRequiredService<IAmABrighterTracer>();
        IAmACommandProcessor processor = provider.GetRequiredService<IAmACommandProcessor>();
        var publishedEvent = new MyEvent();
        using Activity parent = new Activity("publish without producers").SetIdFormat(ActivityIdFormat.W3C);
        parent.ActivityTraceFlags = ActivityTraceFlags.Recorded;
        parent.Start();
        var context = new RequestContext { Span = parent };

        // Act
        if (isAsync)
            await processor.PublishAsync(publishedEvent, context);
        else
            processor.Publish(publishedEvent, context);

        // Assert
        string handlerName = isAsync ? nameof(MyEventHandlerAsync) : nameof(MyEventHandler);
        Assert.Equal(publishedEvent.Id.Value, _receivedMessages[handlerName]);
        Assert.True(tracing.ForceFlush());
        Activity[] spans = [.. _exportedActivities.Where(activity =>
            activity.Source.Name == BrighterSemanticConventions.SourceName
            && Equals(activity.GetTagItem(BrighterSemanticConventions.RequestId), publishedEvent.Id.Value))];
        Assert.Equal(2, spans.Length);
        Activity commandProcessorSpan = Assert.Single(spans, span => span.DisplayName == $"{nameof(MyEvent)} create");
        Activity handlerSpan = Assert.Single(spans, span => span.DisplayName == $"{nameof(MyEvent)} publish");
        Assert.Equal(ActivityKind.Internal, commandProcessorSpan.Kind);
        Assert.Equal(ActivityKind.Internal, handlerSpan.Kind);
        Assert.Equal(parent.TraceId, commandProcessorSpan.TraceId);
        Assert.Equal(parent.SpanId, commandProcessorSpan.ParentSpanId);
        Assert.Equal(commandProcessorSpan.TraceId, handlerSpan.TraceId);
        Assert.Equal(commandProcessorSpan.SpanId, handlerSpan.ParentSpanId);
    }
}
