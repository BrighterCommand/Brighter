namespace Paramore.Brighter.MessagingGateway.Pulsar;

/// <summary>
/// Contains constant definitions for header names used in messaging systems, 
/// particularly for Apache Pulsar integration with Brighter.
/// Includes standard headers, CloudEvents (CE) headers, and custom Brighter-Pulsar headers.
/// </summary>
public static class HeaderNames
{
    /// <summary>Content type of the message payload (e.g., application/json)</summary>
    public const string ContentType  = "ContentType";
    
    /// <summary>Correlation ID for tracing related messages</summary>
    public const string CorrelationId = "CorrelationId";
    
    /// <summary>CloudEvents-formatted unique message identifier</summary>
    public const string MessageId = "CE-EventId";
    
    /// <summary>Number of times a message has been processed/requeued</summary>
    public const string HandledCount = "HandledCount";
    
    /// <summary>Brighter message type classification (e.g., MT_COMMAND, MT_EVENT)</summary>
    public const string MessageType = "MessageType";
    
    /// <summary>Reply destination for request-reply patterns</summary>
    public const string ReplyTo = "ReplyTo";
    
    /// <summary>CloudEvents specification version (e.g., "1.0")</summary>
    public const string SpecVersion = "CE-SpecVersion";
    
    /// <summary>CloudEvents event type descriptor</summary>
    public const string Type = "CE-EventType";
    
    /// <summary>Timestamp of event occurrence in RFC3339 format</summary>
    public const string Time = "CE-EventTime";
    
    /// <summary>CloudEvents subject describing event content</summary>
    public const string Subject = "CE-Subject";
    
    /// <summary>CloudEvents schema URL for payload validation</summary>
    public const string DataSchema = "CE-DataSchema";
    
    /// <summary>CloudEvents source URI identifying event origin</summary>
    public const string Source = "CE-Source";
    
    /// <summary>W3C Trace Context traceparent value</summary>
    public const string TraceParent = "CE-X-TraceParent";
    
    /// <summary>W3C Trace Context tracestate value</summary>
    public const string TraceState = "CE-X-TraceState";
    
    /// <summary>OpenTelemetry baggage items (key-value pairs)</summary>
    public const string Baggage = "CE-X-Baggage";
    
    /// <summary>Original message topic/routing key</summary>
    public const string Topic = "Topic";
    
    /// <summary>Pulsar schema version identifier</summary>
    public const string SchemaVersion = "Brighter-Pulsar-SchemaVersion";
    
    /// <summary>Pulsar message sequence identifier</summary>
    public const string SequenceId = "Brighter-Pulsar-SequenceId";
}
