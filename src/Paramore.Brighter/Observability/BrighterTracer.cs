#region Licence

/* The MIT License (MIT)
Copyright © 2022 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace Paramore.Brighter.Observability;

/// <summary>
/// The Brighter Tracer class abstracts the OpenTelemetry ActivitySource, providing a simple interface to create spans for Brighter
/// </summary>
public class BrighterTracer : IDisposable
{
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="BrighterTracer"/> class.
    /// </summary>
    public BrighterTracer(TimeProvider timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        var assemblyName = typeof(BrighterTracer).Assembly.GetName();
        var sourceName = assemblyName.Name;
        var version = assemblyName.Version?.ToString();
        ActivitySource = new(sourceName ?? BrighterSemanticConventions.SourceName, version ?? "Unknown");
    }

    /// <summary>
    /// The ActivitySource for the tracer
    /// </summary>
    public ActivitySource ActivitySource { get; }

    /// <summary>
    /// Dispose of the ActivitySource
    /// </summary>
    public void Dispose()
    {
        ActivitySource?.Dispose();
    }

    /// <summary>
    /// Create a span for a request in CommandProcessor
    /// </summary>
    /// <param name="span">What type of span are we creating</param>
    /// <param name="request">What is the request that we are tracking with this span</param>
    /// <param name="parentActivity">The parent activity, if any, that we should assign to this span</param>
    /// <param name="links">Are there links to other spans that we should add to this span</param>
    /// <param name="options">How deep should the instrumentation go?</param>
    /// <returns>A span (or dotnet Activity) for the current request</returns>
    public Activity CreateSpan<TRequest>(
        CommandProcessorSpan span, 
        TRequest request, 
        Activity parentActivity = null,
        ActivityLink[] links = null, 
        InstrumentationOptions options = InstrumentationOptions.All
    ) where TRequest : class, IRequest
    {
        var spanName = $"{request.GetType().Name} {span.ToSpanName()}";
        var kind = ActivityKind.Internal;
        var parentId = parentActivity?.Id ?? default;
        var now = _timeProvider.GetUtcNow();

        var tags = new ActivityTagsCollection();
        tags.Add(BrighterSemanticConventions.RequestId, request.Id.ToString());
        tags.Add(BrighterSemanticConventions.RequestType, request.GetType().Name);
        tags.Add(BrighterSemanticConventions.RequestBody, JsonSerializer.Serialize(request));
        tags.Add(BrighterSemanticConventions.Operation, span.ToSpanName());

        var activity = ActivitySource.StartActivity(
            name: spanName,
            kind: kind,
            parentId: parentId,
            tags: tags,
            links: links,
            startTime: now);
        
        Activity.Current = activity;

        return activity;
    }
    
    public static void CreateHandlerEvent(Activity span, string handlerName, bool isAsync, bool isSink = false)
    {
        var tags = new ActivityTagsCollection();
        tags.Add(BrighterSemanticConventions.HandlerName, handlerName);
        tags.Add(BrighterSemanticConventions.HandlerType, isAsync ? "async" : "sync");
        tags.Add(BrighterSemanticConventions.IsSink, isSink);
        
        span.AddEvent(new ActivityEvent(handlerName, DateTimeOffset.UtcNow, tags));
    }
}
