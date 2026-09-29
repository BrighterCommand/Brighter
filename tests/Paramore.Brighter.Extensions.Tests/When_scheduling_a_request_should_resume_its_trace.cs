#region Licence

/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

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

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.Observability;


namespace Paramore.Brighter.Extensions.Tests;

[System.Obsolete]
[NotInParallel]
public class ScheduledRequestTraceTests : IDisposable
{
    private readonly TextMapPropagator _originalPropagator = Propagators.DefaultTextMapPropagator;

    public ScheduledRequestTraceTests()
        => Sdk.SetDefaultTextMapPropagator(new TraceContextPropagator());

    public void Dispose() => Sdk.SetDefaultTextMapPropagator(_originalPropagator);

    public static IEnumerable<(RequestSchedulerType, bool, bool)> ScheduleCases
    {
        get
        {
            var cases = new List<(RequestSchedulerType, bool, bool)>();
            foreach (var operation in new[] { RequestSchedulerType.Send, RequestSchedulerType.Publish, RequestSchedulerType.Post })
            {
                foreach (var isAsync in new[] { false, true })
                {
                    foreach (var useDateTime in new[] { false, true })
                        cases.Add((operation, isAsync, useDateTime));
                }
            }
            return cases;
        }
    }

    [Test]
    [MethodDataSource(nameof(ScheduleCases))]
    public async Task When_scheduling_a_request_should_resume_its_trace(RequestSchedulerType operation, bool isAsync, bool useDateTime)
    {
        //Arrange
        using var tracer = new BrighterTracer();
        var completedSpans = new ConcurrentQueue<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => ReferenceEquals(source, tracer.ActivitySource),
            ActivityStopped = completedSpans.Enqueue,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(listener);
        var timeProvider = new FakeTimeProvider();
        var bus = new InternalBus();
        var syncTopic = new RoutingKey("scheduled-trace.sync");
        var asyncTopic = new RoutingKey("scheduled-trace.async");
        var services = new ServiceCollection();
        services.AddSingleton<IAmABrighterTracer>(tracer);
        services.AddSingleton<ScheduledContextEventHandler>();
        services.AddSingleton<ScheduledContextEventHandlerAsync>();
        services.AddBrighter(options => options.InstrumentationOptions = InstrumentationOptions.RequestInformation)
            .UseScheduler(new InMemorySchedulerFactory { TimeProvider = timeProvider })
            .AddProducers(options => options.ProducerRegistry = new InMemoryProducerRegistryFactory(bus,
                [new Publication { Topic = syncTopic, RequestType = typeof(ScheduledContextEvent) },
                 new Publication { Topic = asyncTopic, RequestType = typeof(ScheduledContextEventAsync) }],
                InstrumentationOptions.RequestInformation).Create())
            .MapperRegistry(_ => { });
        await using var provider = services.BuildServiceProvider();
        var processor = provider.GetRequiredService<IAmACommandProcessor>();
        using var parent = new Activity("original request").SetIdFormat(ActivityIdFormat.W3C).Start();
        parent.TraceStateString = "vendor=value";
        parent.AddBaggage("tenant", "tenant-1");
        var context = new RequestContext { Span = parent };
        var delay = TimeSpan.FromSeconds(1);

        //Act
        if (isAsync)
            await ScheduleAsync(new ScheduledContextEventAsync());
        else
            await ScheduleAsync(new ScheduledContextEvent());
        parent.AddBaggage("tenant", "changed-after-scheduling");
        parent.Stop();
        // Give the worker its own trace even when the test runner supplies an ambient Activity.
        using var unrelated = new Activity("scheduler worker")
            .SetParentId(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded)
            .Start();
        timeProvider.Advance(delay);

        //Assert
        Activity? restoredSpan;
        if (operation == RequestSchedulerType.Post)
        {
            var message = await Assert.That(bus.Stream(isAsync ? asyncTopic : syncTopic)).HasSingleItem();
            await Assert.That(message.Header.TraceParent).IsNotNull();
            await Assert.That(message.Header.TraceParent.Value).Contains(parent.TraceId.ToString());
            await Assert.That(message.Header.TraceState?.Value).IsEqualTo("vendor=value");
            restoredSpan = (await Assert.That(completedSpans.Where(span => span.DisplayName == $"{(isAsync ? asyncTopic : syncTopic)} publish")).HasSingleItem());
        }
        else
        {
            var spans = isAsync ? provider.GetRequiredService<ScheduledContextEventHandlerAsync>().Spans
                : provider.GetRequiredService<ScheduledContextEventHandler>().Spans;
            restoredSpan = (await Assert.That(spans).HasSingleItem());
        }
        await Assert.That(restoredSpan).IsNotNull();
        await Assert.That(restoredSpan).IsNotSameReferenceAs(parent);
        await Assert.That(restoredSpan.TraceId).IsEqualTo(parent.TraceId);
        await Assert.That(restoredSpan.TraceId).IsNotEqualTo(unrelated.TraceId);
        await Assert.That(restoredSpan.TraceStateString).IsEqualTo("vendor=value");
        await Assert.That(restoredSpan.GetBaggageItem("tenant")).IsEqualTo("tenant-1");
        await Assert.That(Activity.Current).IsSameReferenceAs(unrelated);

        async Task ScheduleAsync<TRequest>(TRequest request) where TRequest : class, IRequest
        {
            var at = timeProvider.GetUtcNow().Add(delay);
            _ = (operation, isAsync, useDateTime) switch
            {
                (RequestSchedulerType.Send, false, false) => processor.Send(delay, request, context),
                (RequestSchedulerType.Send, false, true) => processor.Send(at, request, context),
                (RequestSchedulerType.Send, true, false) => await processor.SendAsync(delay, request, context),
                (RequestSchedulerType.Send, true, true) => await processor.SendAsync(at, request, context),
                (RequestSchedulerType.Publish, false, false) => processor.Publish(delay, request, context),
                (RequestSchedulerType.Publish, false, true) => processor.Publish(at, request, context),
                (RequestSchedulerType.Publish, true, false) => await processor.PublishAsync(delay, request, context),
                (RequestSchedulerType.Publish, true, true) => await processor.PublishAsync(at, request, context),
                (RequestSchedulerType.Post, false, false) => processor.Post(delay, request, context),
                (RequestSchedulerType.Post, false, true) => processor.Post(at, request, context),
                (RequestSchedulerType.Post, true, false) => await processor.PostAsync(delay, request, context),
                (RequestSchedulerType.Post, true, true) => await processor.PostAsync(at, request, context),
                _ => throw new ArgumentOutOfRangeException(nameof(operation))
            };
        }
    }
}
