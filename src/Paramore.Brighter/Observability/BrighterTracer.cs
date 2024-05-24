#region Licence

/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Diagnostics;
using System.Linq;
using System.Text.Json;

namespace Paramore.Brighter.Observability;

/// <summary>
/// The Brighter Tracer class abstracts the OpenTelemetry ActivitySource, providing a simple interface to create spans for Brighter
/// </summary>
public class BrighterTracer : IAmABrighterTracer
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
    /// <param name="operation">What type of span are we creating</param>
    /// <param name="request">What is the request that we are tracking with this span</param>
    /// <param name="parentActivity">The parent activity, if any, that we should assign to this span</param>
    /// <param name="links">Are there links to other spans that we should add to this span</param>
    /// <param name="options">How deep should the instrumentation go?</param>
    /// <returns>A span (or dotnet Activity) for the current request named request.name operation.name</returns>
    public Activity CreateSpan<TRequest>(
        CommandProcessorSpanOperation operation, 
        TRequest request, 
        Activity parentActivity = null,
        ActivityLink[] links = null, 
        InstrumentationOptions options = InstrumentationOptions.All
    ) where TRequest : class, IRequest
    {
        var spanName = $"{request.GetType().Name} {operation.ToSpanName()}";
        var kind = ActivityKind.Internal;
        var parentId = parentActivity?.Id;
        var now = _timeProvider.GetUtcNow();

        var tags = new ActivityTagsCollection();
        tags.Add(BrighterSemanticConventions.RequestId, request.Id);
        tags.Add(BrighterSemanticConventions.RequestType, request.GetType().Name);
        tags.Add(BrighterSemanticConventions.RequestBody, JsonSerializer.Serialize(request));
        tags.Add(BrighterSemanticConventions.Operation, operation.ToSpanName());

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

    /// <summary>
    /// Create a span for an outbox operation
    /// </summary>
    /// <param name="info">The attributes of the db operation</param>
    /// <param name="parentActivity">The parent activity, if any, that we should assign to this span</param>
    /// <param name="options">How deep should the instrumentation go?</param>
    /// /// <returns>A new span named either db.operation db.name db.sql.table or db.operation db.name if db.sql.table not available </returns>
    public Activity CreateDbSpan(OutboxSpanInfo info, Activity parentActivity, InstrumentationOptions options)
    {
        var spanName = !string.IsNullOrEmpty(info.dbTable) 
            ? $"{info.dbOperation.ToSpanName()} {info.dbName} {info.dbTable}" : $"{info.dbOperation} {info.dbName}";
        
        var kind = ActivityKind.Client;
        var parentId = parentActivity?.Id;
        var now = _timeProvider.GetUtcNow();

        var tags = new ActivityTagsCollection();
        if (!string.IsNullOrEmpty(info.dbInstanceId)) tags.Add(BrighterSemanticConventions.DbInstanceId, info.dbInstanceId);
        tags.Add(BrighterSemanticConventions.DbName, info.dbName);
        tags.Add(BrighterSemanticConventions.DbOperation, info.dbOperation.ToSpanName());
        tags.Add(BrighterSemanticConventions.DbTable, info.dbTable);
        if (!string.IsNullOrEmpty(info.dbStatement)) tags.Add(BrighterSemanticConventions.DbStatement, info.dbStatement);
        tags.Add(BrighterSemanticConventions.DbSystem, info.dbSystem.ToDbName());
        if (!string.IsNullOrEmpty(info.dbUser)) tags.Add(BrighterSemanticConventions.DbUser, info.dbUser);
        if (!string.IsNullOrEmpty(info.networkPeerAddress)) tags.Add(BrighterSemanticConventions.NetworkPeerAddress, info.networkPeerAddress);
        if (!string.IsNullOrEmpty(info.serverAddress)) tags.Add(BrighterSemanticConventions.ServerAddress, info.serverAddress);
        //NOTE: We convert these to strings because the ActivityTagsCollection seems to only accepts strings?! 
        if (info.networkPeerPort != 0) tags.Add(BrighterSemanticConventions.NetworkPeerPort, info.networkPeerPort.ToString());
        if (info.serverPort != 0) tags.Add(BrighterSemanticConventions.ServerPort, info.serverPort.ToString());
        
        if (info.dbAttributes != null)
           foreach (var pair in info.dbAttributes)
               tags.Add(pair.Key, pair.Value);

        var activity = ActivitySource.StartActivity(
            name: spanName,
            kind: kind,
            parentId: parentId,
            tags: tags,
            startTime: now);
        
        Activity.Current = activity;

        return activity;
    }
 
    /// <summary>
    /// Create an event to denote that we have passed through an event handler
    /// Will be called by the base RequestHandler class's Handle method; invoked at the end of the user defined Handle
    /// method
    /// </summary>
    /// <param name="span">The span to raise an event for</param>
    /// <param name="handlerName">The name of the handler</param>
    /// <param name="isAsync">Is the handler async?</param>
    /// <param name="isSink">Is this the last handler in the chain?</param>
    public static void CreateHandlerEvent(Activity span, string handlerName, bool isAsync, bool isSink = false)
    {
        if (span == null) return;
        
        var tags = new ActivityTagsCollection();
        tags.Add(BrighterSemanticConventions.HandlerName, handlerName);
        tags.Add(BrighterSemanticConventions.HandlerType, isAsync ? "async" : "sync");
        tags.Add(BrighterSemanticConventions.IsSink, isSink);
        
        span.AddEvent(new ActivityEvent(handlerName, DateTimeOffset.UtcNow, tags));
    }

    /// <summary>
    /// Adds an event to a span for each transform/mapper that we pass through in the pipeline
    /// Event is raised before we run the transform
    /// </summary>
    /// <param name="message">The message that we want to transform</param>
    /// <param name="publication">The publication that the message is produced to</param>
    /// <param name="span">The span to add the event to</param>
    /// <param name="mapperName">The name of this mapper</param>
    /// <param name="isAsync">Is this an async pipeline?</param>
    /// <param name="isSink">Is this the mapper, true, or a transform, false?</param>
    public static void CreateMapperEvent(
        Message message, 
        Publication publication, 
        Activity span, 
        string mapperName,
        bool isAsync,
        bool isSink = false)
    {
        if (span == null) return;
        
        var tags = new ActivityTagsCollection();
        tags.Add(BrighterSemanticConventions.MapperName, mapperName);
        tags.Add(BrighterSemanticConventions.MapperType, isAsync ? "async" : "sync");
        tags.Add(BrighterSemanticConventions.IsSink, isSink);
        tags.Add(BrighterSemanticConventions.MessageId, message.Id);
        tags.Add(BrighterSemanticConventions.MessagingDestination, publication.Topic);
        tags.Add(BrighterSemanticConventions.MessagingDestinationPartitionId, message.Header.PartitionKey);
        tags.Add(BrighterSemanticConventions.MessageBodySize, message.Body.Bytes.Length);
        tags.Add(BrighterSemanticConventions.MessageBody, message.Body.Value);
        tags.Add(BrighterSemanticConventions.MessageHeaders, JsonSerializer.Serialize(message.Header));
        
        span.AddEvent(new ActivityEvent(mapperName, DateTimeOffset.UtcNow, tags));
    }

    /// <summary>
    /// Ends a span by correctly setting its status and then disposing of it
    /// </summary>
    /// <param name="span">The span to end</param>
    public void EndSpan(Activity span)
    {
        if (span?.Status == ActivityStatusCode.Unset)
            span.SetStatus(ActivityStatusCode.Ok);
        span?.Dispose();
    }

    /// <summary>
    /// Ends a collection of named spans
    /// </summary>
    /// <param name="handlerSpans"></param>
    public void EndSpans(Dictionary<string, Activity> handlerSpans)
    {
        if (!handlerSpans.Any()) return;
            
        foreach (var handlerSpan in handlerSpans)
        {
            EndSpan(handlerSpan.Value);
        }
    }
}
