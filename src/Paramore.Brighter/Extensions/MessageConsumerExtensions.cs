namespace Paramore.Brighter.Extensions;

public static class MessageConsumerExtensions
{
    /// <summary>
    /// If the consumer implements IUseBrighterDeadLetterSupport set the <see cref="RoutingKey"/> for the dead letter
    /// channel on the consumer, for use in Reject
    ///
    /// If the channel does not implement IUseBrighterDeadLetterSupport this is a no-op
    /// </summary>
    /// <param name="consumer">The consumer to add the dead letter channel too</param>
    /// <param name="deadLetterRoutingKey">The routing key of the dead letter channel</param>
    public static void AddDeadLetterChannel(this IAmAMessageConsumerSync consumer, RoutingKey deadLetterRoutingKey)
    {
        if (consumer is IUseBrighterDeadLetterSupport deadLetterSupport)
            deadLetterSupport.DeadLetterRoutingKey = deadLetterRoutingKey;
    }

    /// <summary>
    /// If the consumer implements IUseBrighterDeadLetterSupport set the <see cref="RoutingKey"/> for the dead letter
    /// channel on the consumer, for use in Reject
    ///
    /// If the channel does not implement IUseBrighterDeadLetterSupport this is a no-op
    /// </summary>
    /// <param name="consumer">The consumer to add the dead letter channel too</param>
    /// <param name="deadLetterRoutingKey">The routing key of the dead letter channel</param>
    public static void AddDeadLetterChannel(this IAmAMessageConsumerAsync consumer, RoutingKey deadLetterRoutingKey)
    {
        if (consumer is IUseBrighterDeadLetterSupport deadLetterSupport) 
            deadLetterSupport.DeadLetterRoutingKey = deadLetterRoutingKey;
    }
    
}
