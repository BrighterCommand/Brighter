namespace Paramore.Brighter.MessagingGateway.Pulsar;

public static class HeaderNames
{
    public const string ContentType  = "ContentType";
    
    public const string CorrelationId = "CorrelationId";
    
    public const string MessageId = "CE-EventId";
    
   
    public const string HandledCount = "HandledCount";
    
    
    public const string MessageType = "MessageType";
    
    
    public const string ReplyTo = "ReplyTo";
    
    
    public const string SpecVersion = "CE-SpecVersion";
    
    
    public const string Type = "CE-EventType";
    
    
    public const string Time = "CE-EventTime";
    
    
    public const string Subject = "CE-Subject";
    
    
    public const string DataSchema = "CE-DataSchema";
    
    
    public const string Source = "CE-Source";
    
    
    public const string TraceParent = "CE-X-TraceParent";

    
    public const string TraceState = "CE-X-TraceState";
    
    public const string Baggage = "CE-X-Baggage";
    
    public const string Topic = "Topic";
    public const string SchemaVersion = "Brighter-Pulsar-SchemaVersion";
    public const string SequenceId = "Brighter-Pulsar-SequenceId";
}
