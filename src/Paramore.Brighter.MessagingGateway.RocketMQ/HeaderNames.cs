namespace Paramore.Brighter.MessagingGateway.RocketMQ;

/// <summary>
/// The name of default header names
/// </summary>
public static class HeaderNames
{
    /// <summary>
    /// The message key
    /// </summary>
    /// <remarks>
    /// If not set, Brighter will set the message id
    /// </remarks>
    public const string Keys = "Brighter-RocketMQ-Keys";
    
    /// <summary>
    /// The message tag 
    /// </summary>
    /// <remarks>
    /// If not set, Brighter will set the tag in the <see cref="RocketMqPublication.Tag"/>
    /// </remarks>
    public const string Tag = "Brighter-RocketMQ-Tag";
    
    /// <summary>
    /// The message type
    /// </summary>
    public const string MessageType = "MessageType";
    
    /// <summary>
    /// The message identifier
    /// </summary>
    public const string MessageId = "CE_id";
    
    /// <summary>
    /// The correlation id
    /// </summary>
    public const string CorrelationId = "CorrelationId";
    
    /// <summary>
    /// The topic
    /// </summary>
    public const string Topic = "Topic";
    
    /// <summary>
    /// The handled count
    /// </summary>
    public const string HandledCount = "HandledCount";
    
    /// <summary>
    /// The time stamp
    /// </summary>
    public const string TimeStamp = "CE_time";
    
    /// <summary>
    /// The subject
    /// </summary>
    public const string Subject = "CE_subject";
    
    /// <summary>
    /// The reply to
    /// </summary>
    public const string ReplyTo = "ReplyTo";
    
    /// <summary>
    /// The content type
    /// </summary>
    public const string ContentType = "Content-Type";
    
    /// <summary>
    /// The Content-Type
    /// </summary>
    public const string DataContentType = "CE_contenttype";
    
    /// <summary>
    /// The source 
    /// </summary>
    public const string Source = "CE_source";
    
    /// <summary>
    /// The spec version 
    /// </summary>
    public const string SpecVersion = "CE_specversion";
    
    /// <summary>
    /// The type 
    /// </summary>
    public const string Type = "CE_type";
    
    /// <summary>
    /// The data schema
    /// </summary>
    public const string DataSchema = "CE_dataschema";

    /// <summary>
    /// The data ref
    /// </summary>
    public const string DataRef = "CE_dataref";
}
