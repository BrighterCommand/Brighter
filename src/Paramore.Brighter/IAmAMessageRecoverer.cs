using System.Collections.Generic;

namespace Paramore.Brighter
{
    /// <summary>
    /// Interface IAmAMessageRecoverer
    /// Used to support reposting a message from a <see cref="IAmAnOutboxSync{T}"/> to a broker via <see cref="IAmAMessageProducer"/>
    /// </summary>
    public interface IAmAMessageRecoverer
    {
        void Repost(List<string> messageIds, IAmAnOutboxSync<Message> outBox, IAmAMessageProducerSync messageProducerSync);
    }
}
