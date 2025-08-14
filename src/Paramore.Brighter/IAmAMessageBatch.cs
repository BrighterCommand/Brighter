using System.Collections.Generic;

namespace Paramore.Brighter;

/// <summary>
/// Class MessageBatch
/// A collection of T <see cref="Message"/>
/// </summary>
public interface IAmAMessageBatch<out TContent> : IAmAMessageBatch
{
    /// <summary>
    /// Collection of messages within the batch, this could either be a Message implementation or a producer specific type like a ServiceBusMessageBatch 
    /// </summary>
    public TContent Content { get; }
}

/// <summary>
/// Class MessageBatch
/// A collection of T <see cref="Message"/>
/// </summary>
public interface IAmAMessageBatch
{
    /// <summary>
    /// Collection of Message Ids within the batch
    /// </summary>
    /// <returns></returns>
    public IEnumerable<Id> Ids();

    /// <summary>
    /// Get the RoutingKey for the batch of messages 
    /// </summary>
    public RoutingKey RoutingKey { get; }
}
