namespace Paramore.Brighter.Transformers.MassTransit;

/// <summary>
/// The MassTransit header used in <see cref="MassTransitTransform"/> and <see cref="MassTransitMessageMapper{TMessage}"/>
/// when looking int <see cref="IRequestContext.Bag"/>
/// </summary>
public static class MassTransitHeaderNames
{
    /// <summary>
    /// The header prefix
    /// </summary>
    public const string HeaderPrefix = "Brighter-MassTranist-";
    
    /// <summary>
    /// The conversation id.
    /// </summary>
    public const string ConversationId = $"{HeaderPrefix}ConversationId"; 
    
    /// <summary>
    /// The destination address.
    /// </summary>
    public const string DestinationAddress = $"{HeaderPrefix}DestinationAddress";
    
    /// <summary>
    /// The expiration time.
    /// </summary>
    public const string ExpirationTime = $"{HeaderPrefix}ExpirationTime";
    
    /// <summary>
    /// The fault address
    /// </summary>
    public const string FaultAddress  = $"{HeaderPrefix}FaultAddress";
    
    /// <summary>
    /// The initiator id
    /// </summary>
    public const string InitiatorId =  $"{HeaderPrefix}InitiatorId";
    
    /// <summary>
    /// The message type.
    /// </summary>
    public const string MessageType = $"{HeaderPrefix}MessageType";
    
    /// <summary>
    /// The request id.
    /// </summary>
    public const string RequestId = $"{HeaderPrefix}RequestId";
    
    /// <summary>
    /// The response address.
    /// </summary>
    public const string ResponseAddress = $"{HeaderPrefix}ResponseAddress";
    
    /// <summary>
    /// The source address.
    /// </summary>
    public const string SourceAddress = $"{HeaderPrefix}SourceAddress";

    /// <summary>
    /// The correlation id
    /// </summary>
    public const string CorrelationId = "CorrelationId";
}
