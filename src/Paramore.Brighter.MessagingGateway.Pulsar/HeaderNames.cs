namespace Paramore.Brighter.MessagingGateway.Pulsar;

public static class HeaderNames
{
    /// <summary>
    /// What is the content type of the message
    /// </summary>
    public const string ContentType  = "ContentType";
    
    /// <summary>
    /// The correlation id
    /// </summary>
    public const string CorrelationId = "CorrelationId";
    
    /// <summary>
    /// The cloud event ID
    /// </summary>
    public const string MessageId = "CE-EventId";
    
    /// <summary>
    /// How many times has the message been retried with a delay
    /// </summary>
    public const string HandledCount = "HandledCount";
    
    /// <summary>
    /// The message type
    /// </summary>
    public const string MessageType = "MessageType";
    
    /// <summary>
    /// Used for a request-reply message to indicate the private channel to reply to
    /// </summary>
    public const string ReplyTo = "ReplyTo";
    
    /// <summary>
    /// The cloud event spec version
    /// </summary>
    public const string SpecVersion = "CE-SpecVersion";
    
    /// <summary>
    /// The cloud event type
    /// </summary>
    public const string Type = "CE-EventType";
    
    /// <summary>
    /// The cloud event time
    /// </summary>
    public const string Time = "CE-EventTime";
    
    /// <summary>
    /// The cloud event subject
    /// </summary>
    public const string Subject = "CE-Subject";
    
    /// <summary>
    /// The cloud event dataschema
    /// </summary>
    public const string DataSchema = "CE-DataSchema";
    
    /// <summary>
    /// The cloud event subject
    /// </summary>
    public const string Source = "CE-Source";
    
    /// <summary>
    /// The cloud events traceparent, follows the W3C standard
    /// </summary>
    public const string TraceParent = "CE-X-TraceParent";

    /// <summary>
    /// The cloud events tracestate, follows the W3C standard
    /// </summary>
    public const string TraceState = "CE-X-TraceState";
    
    public const string Topic = "Topic";
    public const string SchemaVersion = "Brighter-Pulsar-SchemaVersion";
    public const string SequenceId = "Brighter-Pulsar-SequenceId";
}
