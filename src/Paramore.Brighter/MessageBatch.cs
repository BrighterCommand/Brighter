using System.Collections.Generic;
using System.Linq;

namespace Paramore.Brighter;

/// <inheritdoc/>
public class MessageBatch : IAmAMessageBatch<IEnumerable<Message>>
{
    private readonly IEnumerable<Message> _messages;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageBatch"/> class with a collection of messages. 
    /// </summary>
    /// <param name="messages">The collection of messages, usually IEnumerable<message> </param>
    public MessageBatch(IEnumerable<Message> messages)
    {
        _messages = messages;
    }


    /// <inheritdoc/>
    public IEnumerable<Id> Ids() => _messages.Select(x => x.Id);

    /// <inheritdoc/>
    public IEnumerable<Message> Messages => _messages;

    /// <inheritdoc/>
    public RoutingKey RoutingKey => Messages.Select(x => x.Header.Topic).Distinct().Single();
}
