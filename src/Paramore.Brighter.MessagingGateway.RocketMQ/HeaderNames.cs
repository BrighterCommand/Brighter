using System.Diagnostics.CodeAnalysis;

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
    public const string Keys = "Keys";
    
    /// <summary>
    /// The message type
    /// </summary>
    public const string MessageType = "MessageType";
    
    /// <summary>
    /// The message identifier
    /// </summary>
    public const string MessageId = "MessageId";
    
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
    public const string TimeStamp = "TimeStamp";
    
    /// <summary>
    /// The subject
    /// </summary>
    public const string Subject = "Subject";
    
    /// <summary>
    /// The reply to
    /// </summary>
    public const string ReplyTo = "ReplyTo";
    
    /// <summary>
    /// The content type
    /// </summary>
    public const string ContentType = "Content-Type";
    
    /// <summary>
    /// The delay
    /// </summary>
    public const string Delay = "Delay";
}
