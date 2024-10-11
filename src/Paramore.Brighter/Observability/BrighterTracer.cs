﻿#region Licence

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

using OpenTelemetry.Trace;

using System;
using System.Collections.Concurrent;
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
    public BrighterTracer(TimeProvider? timeProvider = null)
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
    /// <param name="operation">The <see cref="CommandProcessorSpanOperation"/> that tells us what type of span are we creating</param>
    /// <param name="request">What is the <see cref="IRequest"/> that we are tracking with this span</param>
    /// <param name="parentActivity">The parent <see cref="Activity"/>, if any, that we should assign to this span</param>
    /// <param name="links">Are there <see cref="ActivityLink"/>s to other spans that we should add to this span</param>
    /// <param name="options">The <see cref="InstrumentationOptions"/> for how deep should the instrumentation go</param>
    /// <returns>A span for the current request named request.name operation.name</returns>
    public Activity? CreateSpan<TRequest>(
        CommandProcessorSpanOperation operation, 
        TRequest request, 
        Activity? parentActivity = null,
        ActivityLink[]? links = null, 
        InstrumentationOptions options = InstrumentationOptions.All
    ) where TRequest : class, IRequest
    {
        var spanName = $"{request.GetType().Name} {operation.ToSpanName()}";
        var kind = ActivityKind.Internal;
        var parentId = parentActivity?.Id;
        var now = _timeProvider.GetUtcNow();

        var tags = new ActivityTagsCollection
        {
            { BrighterSemanticConventions.RequestId, request.Id },
            { BrighterSemanticConventions.RequestType, request.GetType().Name },
            { BrighterSemanticConventions.RequestBody, JsonSerializer.Serialize(request) },
            { BrighterSemanticConventions.Operation, operation.ToSpanName() }
        };

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
    /// Create a span when we consume a message from a queue or stream
    /// </summary>
    /// <param name="operation">How did we obtain the message. InstrumentationOptions.Receive => pull; InstrumentationOptions.Process => push</param>
    /// <param name="message">What is the <see cref="Message"/> that we received; if they have a traceparentid we will use that as a parent for this trace</param>
    /// <param name="messagingSystem">What is the messaging system that we are receiving a message from</param>
    /// <param name="options">The <see cref="InstrumentationOptions"/> for how deep should the instrumentation go</param>
    /// <returns></returns>
    public Activity? CreateSpan(
       MessagePumpSpanOperation operation,
       Message message,
       MessagingSystem messagingSystem,
       InstrumentationOptions options = InstrumentationOptions.All
    )
    {
        var spanName = $"{message.Header.Topic} {operation.ToSpanName()}";
        var kind = ActivityKind.Consumer;
        var parentId = message.Header.TraceParent;
        var now = _timeProvider.GetUtcNow();

        var tags = new ActivityTagsCollection()
        {
            { BrighterSemanticConventions.MessagingOperationType, operation.ToSpanName() },
            { BrighterSemanticConventions.MessagingDestination, message.Header.Topic },
            { BrighterSemanticConventions.MessagingDestinationPartitionId, message.Header.PartitionKey },
            { BrighterSemanticConventions.MessageId, message.Id },
            { BrighterSemanticConventions.MessageType, message.Header.MessageType.ToString() },
            { BrighterSemanticConventions.MessageBodySize, message.Body.Bytes.Length },
            { BrighterSemanticConventions.MessageBody, message.Body.Value },
            { BrighterSemanticConventions.MessageHeaders, JsonSerializer.Serialize(message.Header) },
            { BrighterSemanticConventions.ConversationId, message.Header.CorrelationId },
            { BrighterSemanticConventions.MessagingSystem, messagingSystem.ToMessagingSystemName() },
            { BrighterSemanticConventions.CeMessageId, message.Id },
            { BrighterSemanticConventions.CeSource, message.Header.Source },
            { BrighterSemanticConventions.CeVersion, "1.0"},
            { BrighterSemanticConventions.CeSubject, message.Header.Subject },
            { BrighterSemanticConventions.CeType, message.Header.Type},
            { BrighterSemanticConventions.ReplyTo, message.Header.ReplyTo },
            { BrighterSemanticConventions.HandledCount, message.Header.HandledCount }
            
        };
        
        var activity = ActivitySource.StartActivity(
            name: spanName,
            kind: kind,
            parentId: parentId,
            tags: tags,
            startTime: now);
        
        
        activity?.AddBaggage("correlationId", message.Header.CorrelationId);
        
        Activity.Current = activity;

        return activity;
    }

    /// <summary>
    /// Create a span for a request in CommandProcessor
    /// </summary>
    /// <param name="parentActivity">The parent <see cref="Activity"/>, if any, that we should assign to this span</param>
    /// <param name="links">Are there <see cref="ActivityLink"/> to other spans that we should add to this span</param>
    /// <param name="options">The <see cref="InstrumentationOptions"/> for how deep should the instrumentation go?</param>
    /// <returns>A span (or dotnet Activity) for the current request named request.name operation.name</returns>
    public Activity? CreateBatchSpan<TRequest>(
        Activity? parentActivity = null,
        ActivityLink[]? links = null, 
        InstrumentationOptions options = InstrumentationOptions.All
    ) where TRequest : class, IRequest
    {
        var requestType = typeof(TRequest);
        var operation = CommandProcessorSpanOperation.Create;
        
        var spanName = $"{requestType.Name} {operation.ToSpanName()}";
        var kind = ActivityKind.Internal;
        var parentId = parentActivity?.Id;
        var now = _timeProvider.GetUtcNow();

        var tags = new ActivityTagsCollection
        {
            { BrighterSemanticConventions.RequestType, requestType.Name },
            { BrighterSemanticConventions.Operation, operation.ToSpanName() }
        };

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
    /// The parent span for the message pump. This is the entry point for the message pump
    /// </summary>
    /// <param name="operation">The <see cref="MessagePumpSpanOperation"/>. This should be Begin or End</param>
    /// <param name="topic">The <see cref="RoutingKey"/> for this span</param>
    /// <param name="messagingSystem">The <see cref="MessagingSystem"/> that we are receiving from</param>
    /// <param name="options">The <see cref="InstrumentationOptions"/> for how deep should the instrumentation go?</param>
    /// <returns>A span (or dotnet Activity) for the current request named request.name operation.name</returns>
    public Activity? CreateMessagePumpSpan(
        MessagePumpSpanOperation operation,
        RoutingKey topic,
        MessagingSystem messagingSystem,
        InstrumentationOptions options = InstrumentationOptions.All)
    {
        if (operation != MessagePumpSpanOperation.Begin)
            throw new ArgumentOutOfRangeException(nameof(operation), "Operation must be Begin or End");
        
        var spanName = $"{topic} {operation.ToSpanName()}";
        var kind = ActivityKind.Consumer;
        var now = _timeProvider.GetUtcNow();

        var tags = new ActivityTagsCollection()
        {
            { BrighterSemanticConventions.MessagingSystem, messagingSystem.ToMessagingSystemName() },
            { BrighterSemanticConventions.MessagingDestination, topic },
            { BrighterSemanticConventions.Operation, operation.ToSpanName() }
        };
        
        Activity? activity = ActivitySource.StartActivity(kind: kind, tags: tags, links: null, startTime: now, name: spanName);
        
        if(activity is not null)
            Activity.Current = activity;

        return activity; 
    }

    /// <summary>
    /// When there is a failure during message processing we need to create a span for that message failure
    /// as we don't have a message to derive the span details for
    /// </summary>
    /// <param name="messagePumpException"></param>
    /// <param name="topic">The <see cref="RoutingKey"/> for this span</param>
    /// <param name="operation">The <see cref="MessagingSystem"/> we were trying to perform</param>
    /// <param name="messagingSystem">The <see cref="MessagingSystem"/> that we are receiving from</param>
    /// <param name="options">The <see cref="InstrumentationOptions"/> for how deep should the instrumentation go?</param>
    /// <returns>A span (or dotnet Activity) for the current message named topic operation.name</returns>
    public Activity? CreateMessagePumpExceptionSpan(
        Exception messagePumpException,
        RoutingKey topic,
        MessagePumpSpanOperation operation,
        MessagingSystem messagingSystem,
        InstrumentationOptions options = InstrumentationOptions.All)
    {
        var spanName = $"{topic} {operation.ToSpanName()}";
        var kind = ActivityKind.Consumer;
        var now = _timeProvider.GetUtcNow();

        var tags = new ActivityTagsCollection()
        {
            { BrighterSemanticConventions.MessagingOperationType, operation.ToSpanName() },
            { BrighterSemanticConventions.MessagingSystem, messagingSystem.ToMessagingSystemName() },
            { BrighterSemanticConventions.MessagingDestination, topic },
            { BrighterSemanticConventions.Operation, operation.ToSpanName() }
        };
        
       Activity? activity;
        if (Activity.Current != null)
            activity = ActivitySource.StartActivity(name: spanName, kind: kind, parentContext: Activity.Current.Context, tags: tags, links: null,  now);
        else
            activity = ActivitySource.StartActivity(kind: kind, tags: tags, links: null, startTime: now, name: spanName);
        
        activity?.RecordException(messagePumpException);
        activity?.SetStatus(ActivityStatusCode.Error, messagePumpException.Message);
        
        if(activity is not null)
            Activity.Current = activity;

        return activity;
    }
    
    public Activity? CreateArchiveSpan(
        Activity? parentActivity,
        string? messageId = null,
        InstrumentationOptions options = InstrumentationOptions.All)
    {
        var spanName = $"{BrighterSemanticConventions.ArchiveMessages} {CommandProcessorSpanOperation.Archive.ToSpanName()}";
        var kind = ActivityKind.Producer;
        var parentId = parentActivity?.Id;
        var now = _timeProvider.GetUtcNow();  
        
        var activity = ActivitySource.StartActivity(
            name: spanName,
            kind: kind,
            parentId: parentId,
            startTime: now);
        
        Activity.Current = activity;

        return activity;
    }

    /// <summary>
    /// Create a span for a batch of messages to be cleared  
    /// </summary>
    /// <param name="operation">The <see cref="CommandProcessorSpanOperation"/> being performed as part of the Clear Span</param>
    /// <param name="parentActivity">What is the parent <see cref="Activity"/></param>
    /// <param name="messageId">What is the identifier of the message we are trying to clear</param>
    /// <param name="options">The <see cref="InstrumentationOptions"/> for how verbose do we want to be?</param>
    /// <returns></returns>
    public Activity? CreateClearSpan(
        CommandProcessorSpanOperation operation,
        Activity? parentActivity,
        string? messageId = null,
        InstrumentationOptions options = InstrumentationOptions.All)
    {
        var spanName = $"{BrighterSemanticConventions.ClearMessages} {operation.ToSpanName()}";
        var kind = ActivityKind.Producer;
        var parentId = parentActivity?.Id;
        var now = _timeProvider.GetUtcNow(); 
        
        var tags = new ActivityTagsCollection
        {
            { BrighterSemanticConventions.Operation, CommandProcessorSpanOperation.Clear.ToSpanName() }
        };
        
        if (!string.IsNullOrEmpty(messageId)) tags.Add(BrighterSemanticConventions.MessageId, messageId);
        
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
    /// Create a span for an outbox operation
    /// </summary>
    /// <param name="info">An <see cref="OutboxSpanInfo"/> with the attributes of the db operation</param>
    /// <param name="parentActivity">The parent <see cref="Activity"/>, if any, that we should assign to this span</param>
    /// <param name="options">The <see cref="InstrumentationOptions"/> that explain how deep should the instrumentation go?</param>
    /// /// <returns>A new span named either db.operation db.name db.sql.table or db.operation db.name if db.sql.table not available </returns>
    public Activity? CreateDbSpan(OutboxSpanInfo info, Activity? parentActivity, InstrumentationOptions options)
    {
        var spanName = !string.IsNullOrEmpty(info.dbTable) 
            ? $"{info.dbOperation.ToSpanName()} {info.dbName} {info.dbTable}" : $"{info.dbOperation} {info.dbName}";
        
        var kind = ActivityKind.Client;
        var parentId = parentActivity?.Id;
        var now = _timeProvider.GetUtcNow();

        var tags = new ActivityTagsCollection
        {
            { BrighterSemanticConventions.DbName, info.dbName },
            { BrighterSemanticConventions.DbOperation, info.dbOperation.ToSpanName() },
            { BrighterSemanticConventions.DbTable, info.dbTable },
            { BrighterSemanticConventions.DbSystem, info.dbSystem.ToDbName() }
        };

        if (!string.IsNullOrEmpty(info.dbStatement)) tags.Add(BrighterSemanticConventions.DbStatement, info.dbStatement);
        if (!string.IsNullOrEmpty(info.dbInstanceId)) tags.Add(BrighterSemanticConventions.DbInstanceId, info.dbInstanceId);
        if (!string.IsNullOrEmpty(info.dbUser)) tags.Add(BrighterSemanticConventions.DbUser, info.dbUser);
        if (!string.IsNullOrEmpty(info.networkPeerAddress)) tags.Add(BrighterSemanticConventions.NetworkPeerAddress, info.networkPeerAddress);
        if (!string.IsNullOrEmpty(info.serverAddress)) tags.Add(BrighterSemanticConventions.ServerAddress, info.serverAddress);
        if (info.networkPeerPort != 0) tags.Add(BrighterSemanticConventions.NetworkPeerPort, info.networkPeerPort);
        if (info.serverPort != 0) tags.Add(BrighterSemanticConventions.ServerPort, info.serverPort);
        
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
    /// Create a span that represents Brighter producing a message to a channel
    /// </summary>
    /// <param name="publication">The <see cref="Publication"/> which represents where we are sending the message</param>
    /// <param name="message">The <see cref="Message"/> that we are sending</param>
    /// <param name="parentActivity">The parent <see cref="Activity"/>, if any, that we should assign to this span</param>
    /// <param name="instrumentationOptions"> The <see cref="InstrumentationOptions"/> for how deep should the instrumentation go?</param>
    /// <returns>A new span named channel publish</returns>
    public Activity? CreateProducerSpan(
        Publication publication, 
        Message? message, 
        Activity? parentActivity,
        InstrumentationOptions instrumentationOptions = InstrumentationOptions.All
    )
    {
        var spanName = $"{publication.Topic} {CommandProcessorSpanOperation.Publish.ToSpanName()}";
        
        var kind = ActivityKind.Producer;
        var parentId = parentActivity?.Id;
        var now = _timeProvider.GetUtcNow();

        var tags = new ActivityTagsCollection
        {
            //OTel specification attributes
            { BrighterSemanticConventions.MessagingOperationType, CommandProcessorSpanOperation.Publish.ToSpanName() },
            
            //cloud events attributes
            { BrighterSemanticConventions.CeSource, publication.Source },
            { BrighterSemanticConventions.CeVersion, "1.0" },
            { BrighterSemanticConventions.CeSubject, publication.Subject },
            { BrighterSemanticConventions.CeType, publication.Type }
        };

        if (message is not null)
        {
            //OTel specification attributes
            tags.Add(BrighterSemanticConventions.MessageId, message.Id);
            tags.Add(BrighterSemanticConventions.MessageType, message.Header.MessageType.ToString());
            tags.Add(BrighterSemanticConventions.MessagingDestination, publication.Topic);
            tags.Add(BrighterSemanticConventions.MessagingDestinationPartitionId, message.Header.PartitionKey);
            tags.Add(BrighterSemanticConventions.MessageBodySize, message.Body.Bytes.Length);
            tags.Add(BrighterSemanticConventions.MessageBody, message.Body.Value);
            tags.Add(BrighterSemanticConventions.MessageHeaders, JsonSerializer.Serialize(message.Header));
            tags.Add(BrighterSemanticConventions.ConversationId, message.Header.CorrelationId); 
            
            //cloud events attributes
            tags.Add(BrighterSemanticConventions.CeMessageId, message.Id);
        }

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
    /// NOTE: Events are static, as we only need the instance state to create an activity
    /// </summary>
    /// <param name="span">The <see cref="Activity"/> to raise an event for</param>
    /// <param name="handlerName">The name of the handler</param>
    /// <param name="isAsync">Is the handler async?</param>
    /// <param name="isSink">Is this the last handler in the chain?</param>
    public static void WriteHandlerEvent(Activity? span, string handlerName, bool isAsync, bool isSink = false)
    {
        if (span == null) return;
        
        var tags = new ActivityTagsCollection
        {
            { BrighterSemanticConventions.HandlerName, handlerName },
            { BrighterSemanticConventions.HandlerType, isAsync ? "async" : "sync" },
            { BrighterSemanticConventions.IsSink, isSink }
        };

        span.AddEvent(new ActivityEvent(handlerName, DateTimeOffset.UtcNow, tags));
    }

    /// <summary>
    /// Adds an event to a span for each transform/mapper that we pass through in the pipeline
    /// Event is raised before we run the transform
    /// NOTE: Events are static, as we only need the instance state to create an activity
    /// </summary>
    /// <param name="message">The <see cref="Message"/> that we want to transform</param>
    /// <param name="publication">The <see cref="Publication"/> that the message is produced to</param>
    /// <param name="span">The <see cref="Activity"/> to add the event to</param>
    /// <param name="mapperName">The name of this mapper</param>
    /// <param name="isAsync">Is this an async pipeline?</param>
    /// <param name="isSink">Is this the mapper, true, or a transform, false?</param>
    public static void WriteMapperEvent(
        Message message, 
        Publication publication, 
        Activity? span, 
        string mapperName,
        bool isAsync,
        bool isSink = false)
    {
        if (span == null) return;
        
        var tags = new ActivityTagsCollection
        {
            { BrighterSemanticConventions.MapperName, mapperName },
            { BrighterSemanticConventions.MapperType, isAsync ? "async" : "sync" },
            { BrighterSemanticConventions.IsSink, isSink },
            { BrighterSemanticConventions.MessageId, message.Id },
            { BrighterSemanticConventions.MessagingDestination, publication.Topic },
            { BrighterSemanticConventions.MessagingDestinationPartitionId, message.Header.PartitionKey },
            { BrighterSemanticConventions.MessageBodySize, message.Body.Bytes.Length },
            { BrighterSemanticConventions.MessageBody, message.Body.Value },
            { BrighterSemanticConventions.MessageHeaders, JsonSerializer.Serialize(message.Header) }
        };

        span.AddEvent(new ActivityEvent(mapperName, DateTimeOffset.UtcNow, tags));
    }

    /// <summary>
    /// Create an event representing the external service bus calling the outbox
    /// This is generic and not specific details from a particular outbox and is thus mostly message properties
    /// NOTE: Events are static, as we only need the instance state to create an activity
    /// </summary>
    /// <param name="operation">What <see cref="OutboxDbOperation"/> are we performing on the Outbox</param>
    /// <param name="message">What is the <see cref="Message"/> we want to write to the Outbox or have read from the Outbox</param>
    /// <param name="span">What is the parent <see cref="Activity"/> for this event</param>
    /// <param name="isSharedTransaction">Does this from part of a shared transaction with handler code?</param>
    /// <param name="isAsync">Is the handler writing async?</param>
    /// <param name="instrumentationOptions"> <see cref="InstrumentationOptions"/> for how verbose should our instrumentation be</param>
    public static void WriteOutboxEvent(
        OutboxDbOperation operation, 
        Message message, 
        Activity? span,
        bool isSharedTransaction, 
        bool isAsync, 
        InstrumentationOptions instrumentationOptions
    ) 
    {
        if (span == null) return;
        
        var outBoxType = isAsync ? "async" : "sync";
        
        var tags = new ActivityTagsCollection
        {
            { BrighterSemanticConventions.OutboxSharedTransaction, isSharedTransaction },
            { BrighterSemanticConventions.OutboxType, outBoxType },
            { BrighterSemanticConventions.MessageId, message.Id },
            { BrighterSemanticConventions.MessagingDestination, message.Header.Topic },
            { BrighterSemanticConventions.MessageBodySize, message.Body.Bytes.Length },
            { BrighterSemanticConventions.MessageBody, message.Body.Value },
            { BrighterSemanticConventions.MessageType, message.Header.MessageType.ToString() },
            { BrighterSemanticConventions.MessagingDestinationPartitionId, message.Header.PartitionKey },
            { BrighterSemanticConventions.MessageHeaders, JsonSerializer.Serialize(message.Header) }
        };

        span.AddEvent(new ActivityEvent(operation.ToSpanName(), DateTimeOffset.UtcNow, tags));
 
    }
    
    /// <summary>
    /// Create an event representing the external service bus calling the outbox
    /// This is generic and not specific details from a particular outbox and is thus mostly message properties
    /// This is a batch version of <see cref="WriteOutbox"/>
    /// NOTE: Events are static, as we only need the instance state to create an activity
    /// </summary>
    /// <param name="operation">What <see cref="OutboxDbOperation"/> are we performing on the group of messages</param>
    /// <param name="messages">The set of <see cref="Message"/>s we want to record the event for</param>
    /// <param name="span">The <see cref="Activity"/> we are adding the <see cref="ActivityEvent"/> to</param>
    /// <param name="isSharedTransaction">Are we using a shared transaction with the application to write to the Outbox</param>
    /// <param name="isAsync">Is this an async operation</param>
    /// <param name="instrumentationOptions">What <see cref="InstrumentationOptions"/> have we set to control verbosity</param>
    public static void WriteOutboxEvent(
        OutboxDbOperation operation, 
        IEnumerable<Message> messages, 
        Activity? span, 
        bool isSharedTransaction, 
        bool isAsync, 
        InstrumentationOptions instrumentationOptions)
    {
        if (span == null) return;
        
        foreach (var message in messages)
            WriteOutboxEvent(operation, message, span, isSharedTransaction, isAsync, instrumentationOptions); 
    }

    /// <summary>
    /// Writes a producer event to the current span
    /// This is generic and requires details of the message and the transport (messaging system)
    /// NOTE: Events are static, as we only need the instance state to create an activity
    /// </summary>
    /// <param name="span">The owning <see cref="Activity"/> to which we will write the event; nothing written if null</param>
    /// <param name="messagingSystem">Which <see cref="MessagingSystem"/> is the producer</param>
    /// <param name="message">The <see cref="Message"/> being produced</param>
    public static void WriteProducerEvent(Activity? span, MessagingSystem messagingSystem, Message message)
    {
        if (span == null) return;
        
        var tags = new ActivityTagsCollection
        {
            { BrighterSemanticConventions.MessagingOperationType, CommandProcessorSpanOperation.Publish.ToSpanName() },
            { BrighterSemanticConventions.MessagingSystem, messagingSystem.ToMessagingSystemName() },
            { BrighterSemanticConventions.MessagingDestination, message.Header.Topic },
            { BrighterSemanticConventions.MessagingDestinationPartitionId, message.Header.PartitionKey },
            { BrighterSemanticConventions.MessageId, message.Id },
            { BrighterSemanticConventions.MessageHeaders, JsonSerializer.Serialize(message.Header) },
            { BrighterSemanticConventions.MessageType, message.Header.MessageType.ToString() },
            { BrighterSemanticConventions.MessageBodySize, message.Body.Bytes.Length },
            { BrighterSemanticConventions.MessageBody, message.Body.Value },
            { BrighterSemanticConventions.ConversationId, message.Header.CorrelationId },
            
            { BrighterSemanticConventions.CeMessageId, message.Id },
            { BrighterSemanticConventions.CeSource, message.Header.Source },
            { BrighterSemanticConventions.CeVersion, "1.0"},
            { BrighterSemanticConventions.CeSubject, message.Header.Subject },
            { BrighterSemanticConventions.CeType, message.Header.Type }
        };

        span.AddEvent(new ActivityEvent($"{message.Header.Topic} {CommandProcessorSpanOperation.Publish.ToSpanName()}", DateTimeOffset.UtcNow, tags));
    }
 
    /// <summary>
    /// Ends a span by correctly setting its status and then disposing of it
    /// </summary>
    /// <param name="span">The span to end</param>
    public void EndSpan(Activity? span)
    {
        if (span?.Status == ActivityStatusCode.Unset)
            span.SetStatus(ActivityStatusCode.Ok);
        span?.Dispose();
    }

    /// <summary>
    /// Ends a collection of named spans
    /// </summary>
    /// <param name="handlerSpans"></param>
    public void EndSpans(ConcurrentDictionary<string, Activity> handlerSpans)
    {
        if (!handlerSpans.Any()) return;
            
        foreach (var handlerSpan in handlerSpans)
        {
            EndSpan(handlerSpan.Value);
        }
    }

    /// <summary>
    /// Links together a collection of spans
    /// Mainly used with a batch to link siblings to each other
    /// </summary>
    /// <param name="handlerSpans"></param>
    public void LinkSpans(ConcurrentDictionary<string, Activity> handlerSpans)
    {
        if (!handlerSpans.Any()) return;
          
        var handlerNames = handlerSpans.Keys.ToList();
        foreach (var handlerName in handlerNames)
        {
            //var handlerSpan = handlerSpans[handlerName];
            foreach (var hs in handlerSpans)
            {
                if (hs.Key != handlerName)
                {
                    //TODO: Needs adding when https://github.com/dotnet/runtime/pull/101381 is released  
                    //handlerspan.AddLink(new ActivityLink(handlerspan.Value.Context));
                }
            }
        }
    }


}
