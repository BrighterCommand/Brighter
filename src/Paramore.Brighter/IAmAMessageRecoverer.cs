using System.Collections.Generic;

namespace Paramore.Brighter
{
    /// <summary>
    /// Interface IAmAMessageRecoverer
    /// Used to support reposting a message from a <see cref="IAmAnOutboxSync{T}"/> to a broker via <see cref="IAmAMessageProducer"/>
    /// </summary>
    public interface IAmAMessageRecoverer
    {
        void Repost<T, TTransaction>(
            List<string> messageIds, 
            IAmAnOutboxSync<T, TTransaction> outBox, 
            IAmAMessageProducerSync messageProducerSync
            ) where T : Message;
    }
}
